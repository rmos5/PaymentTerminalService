using Newtonsoft.Json;
using PaymentTerminalService.Model;
using PaymentTerminalService.Terminals;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentTerminalService.Host
{
    /// <summary>
    /// Manages the catalog of available payment terminals, selection state, and provides all selection/discovery operations.
    /// Loads and persists catalog data (including selected terminal) from a JSON configuration file matching the TerminalCatalogResponse schema.
    /// Implements <see cref="IPaymentTerminalSelector"/> for terminal discovery, selection, and state management.
    /// </summary>
    internal partial class PaymentTerminalManagementService : IPaymentTerminalSelector, IDisposable
    {
        /// <summary>
        /// JSON file name used to persist terminal catalog data.
        /// </summary>
        private const string CatalogFileName = "terminals.json";

        private readonly object sync = new object();

        private readonly string terminalSessionDirectoryBase;

        private IPaymentTerminal selectedPaymentTerminal = null;

        private TerminalCatalogResponse terminalCatalogResponse;

        private const string VerifoneYomaniXR_001 = "VerifoneYomaniXR-001";

        private const string TestTerminal_001 = "TestTerminal-001";

        /// <summary>
        /// Static catalog definition used as the authoritative source of known terminals.
        /// On each startup this definition is merged with the persisted <see cref="CatalogFileName"/>:
        /// user-configured settings (port, connection, vendor payload) are preserved for terminals
        /// that already exist in the file; new terminals are added; terminals no longer present in
        /// this definition are removed from the file.
        /// </summary>
        /// <remarks>
        /// The test terminal is only included in DEBUG builds.
        /// The TCP and Bluetooth connection entries on the test terminal are configuration
        /// samples only — <see cref="TestPaymentTerminal"/> does not use them at runtime.
        /// </remarks>
        private static readonly TerminalCatalogResponse catalogDefinition = new TerminalCatalogResponse
        {
            Terminals = new List<TerminalDescriptor>
            {
                new TerminalDescriptor
                {
                    TerminalId = VerifoneYomaniXR_001,
                    Vendor = VerifoneYomaniXRTerminal.VendorString,
                    Model = VerifoneYomaniXRTerminal.ModelString,
                    DisplayName = "Yomani Payment Terminal",
                    Connections = new List<TerminalConnectionOption>
                    {
                        new TerminalConnectionOption
                        {
                            ConnectionId = "Serial-001",
                            ConnectionType = ConnectionType.Serial,
                            DisplayName = "Serial connection",
                            Serial = new SerialConnection { PortName = "" },
                            VendorPayload = new VendorPayload()
                        }
                    },
                    IsLoyaltySupported = true,
                    VendorPayload = new VendorPayload(),
                    SelectedConnectionId = null
                },
#if DEBUG // Include test terminal in debug builds only
                new TerminalDescriptor
                {
                    TerminalId = TestTerminal_001,
                    Vendor = TestPaymentTerminal.VendorString,
                    Model = TestPaymentTerminal.ModelString,
                    DisplayName = "Test Payment Terminal",
                    Connections = new List<TerminalConnectionOption>
                    {
                        new TerminalConnectionOption
                        {
                            ConnectionId = "Serial-001",
                            ConnectionType = ConnectionType.Serial,
                            DisplayName = "Serial connection",
                            Serial = new SerialConnection { PortName = "" },
                            VendorPayload = new VendorPayload()
                        },
                        new TerminalConnectionOption
                        {
                            ConnectionId = "Tcp-001",
                            ConnectionType = ConnectionType.Tcp,
                            DisplayName = "Tcp connection",
                            Tcp = new TcpConnection { Host = "", Port = 0 },
                            VendorPayload = new VendorPayload()
                        },
                        new TerminalConnectionOption
                        {
                            ConnectionId = "Bluetooth-001",
                            ConnectionType = ConnectionType.Bluetooth,
                            DisplayName = "Bluetooth connection",
                            Bluetooth = new BluetoothConnection { MacAddress = "" },
                            VendorPayload = new VendorPayload()
                        }
                    },
                    IsLoyaltySupported = true,
                    VendorPayload = new VendorPayload(),
                    SelectedConnectionId = null
                }
#endif
            },
            SelectedTerminalId = null
        };

        /// <inheritdoc/>
        public IPaymentTerminal SelectedPaymentTerminal
        {
            get
            {
                lock (sync)
                {
                    return selectedPaymentTerminal;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentTerminalManagementService"/> class.
        /// Loads terminal catalog and selection state from <see cref="CatalogFileName"/>.
        /// If the file is missing or invalid, falls back to <see cref="catalogDefinition"/> and creates the file.
        /// </summary>
        /// <param name="terminalSessionDirectoryBase">
        /// Base directory under which per-terminal session storage subdirectories are created.
        /// </param>
        public PaymentTerminalManagementService(string terminalSessionDirectoryBase)
        {
            lock (sync)
            {
                terminalCatalogResponse = LoadOrCreateCatalog();
            }

            this.terminalSessionDirectoryBase = terminalSessionDirectoryBase;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Trace.WriteLine($"{nameof(Dispose)}", GetType().FullName);

            IDisposable disposableTerminal = null;
            lock (sync)
            {
                disposableTerminal = selectedPaymentTerminal as IDisposable;
                selectedPaymentTerminal = null;
            }

            if (disposableTerminal != null)
            {
                try
                {
                    disposableTerminal.Dispose();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to dispose selected payment terminal: {ex}", GetType().FullName);
                }
            }
        }

        /// <summary>
        /// Loads the terminal catalog from <see cref="CatalogFileName"/> and merges it with
        /// <see cref="catalogDefinition"/>. User-configured settings are preserved for terminals
        /// that exist in both sources. Terminals absent from the definition are dropped.
        /// If the file is missing or cannot be parsed, the static definition is used directly.
        /// The resulting catalog is always saved back to disk.
        /// </summary>
        private TerminalCatalogResponse LoadOrCreateCatalog()
        {
            Trace.WriteLine($"{nameof(LoadOrCreateCatalog)}", GetType().FullName);
            TerminalCatalogResponse catalog = null;

            try
            {
                if (File.Exists(CatalogFileName))
                {
                    Trace.WriteLine($"Catalog file {CatalogFileName} found. Reading...", GetType().FullName);
                    var json = File.ReadAllText(CatalogFileName);
                    catalog = JsonConvert.DeserializeObject<TerminalCatalogResponse>(json);
                    if (catalog != null && catalog.Terminals != null)
                    {
                        // Synchronize with catalogDefinition
                        var defTerminals = catalogDefinition.Terminals.ToDictionary(t => t.TerminalId);
                        var catTerminals = catalog.Terminals.ToDictionary(t => t.TerminalId);

                        // Build new catalog, preserving settings from serialized file where possible
                        var mergedTerminals = new List<TerminalDescriptor>();
                        foreach (var def in defTerminals.Values)
                        {
                            if (catTerminals.TryGetValue(def.TerminalId, out var existing))
                            {
                                mergedTerminals.Add(existing); // preserve user settings
                                Trace.WriteLine($"Preserved terminal from file: {def.TerminalId}", GetType().FullName);
                            }
                            else
                            {
                                mergedTerminals.Add(def);
                                Trace.WriteLine($"Added new terminal definition: {def.TerminalId}", GetType().FullName);
                            }
                        }

                        // Remove terminals not in static definition
                        var toRemove = catTerminals.Keys.Except(defTerminals.Keys).ToList();
                        if (toRemove.Count > 0)
                        {
                            Trace.WriteLine($"Removed terminals not in static definition: {string.Join(", ", toRemove)}", GetType().FullName);
                        }

                        catalog.Terminals = mergedTerminals;

                        // Reset SelectedTerminalId if the previously selected terminal was removed.
                        if (!string.IsNullOrWhiteSpace(catalog.SelectedTerminalId) &&
                            !catalog.Terminals.Any(t => t.TerminalId == catalog.SelectedTerminalId))
                        {
                            catalog.SelectedTerminalId = null;
                            Trace.WriteLine("SelectedTerminalId reset due to missing terminal.", GetType().FullName);
                        }

                        SaveCatalog(catalog);
                        return catalog;
                    }
                    Trace.WriteLine("Catalog file is empty or invalid, using fallback", GetType().FullName);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to load catalog from {CatalogFileName}: {ex}", GetType().FullName);
            }

            // Fallback to static definition and create file
            Trace.WriteLine("Using static catalog definition and creating file", GetType().FullName);
            SaveCatalog(catalogDefinition);
            return catalogDefinition;
        }

        /// <summary>
        /// Serializes <paramref name="catalog"/> to <see cref="CatalogFileName"/> as indented JSON.
        /// Failures are traced and swallowed so a write error never interrupts the calling flow.
        /// </summary>
        /// <param name="catalog">The catalog to persist.</param>
        private void SaveCatalog(TerminalCatalogResponse catalog)
        {
            Trace.WriteLine($"{nameof(SaveCatalog)}", GetType().FullName);
            try
            {
                var json = JsonConvert.SerializeObject(catalog, Formatting.Indented);
                File.WriteAllText(CatalogFileName, json);
                Trace.WriteLine($"Catalog saved to {CatalogFileName}", GetType().FullName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to save catalog to {CatalogFileName}: {ex}", GetType().FullName);
            }
        }

        /// <inheritdoc/>
        public Task<TerminalCatalogResponse> GetTerminalsAsync(CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(GetTerminalsAsync)}", GetType().FullName);
            TerminalCatalogResponse catalog;
            lock (sync)
            {
                catalog = terminalCatalogResponse;
            }
            return Task.FromResult(catalog);
        }

        /// <inheritdoc/>
        public Task<SelectedTerminalResponse> GetSelectedTerminalAsync(CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(GetSelectedTerminalAsync)}", GetType().FullName);
            lock (sync)
            {
                var selectedId = terminalCatalogResponse.SelectedTerminalId;
                if (string.IsNullOrWhiteSpace(selectedId))
                {
                    Trace.WriteLine("No terminal selected", GetType().FullName);
                    return Task.FromResult<SelectedTerminalResponse>(null);
                }

                var selected = terminalCatalogResponse.Terminals?.FirstOrDefault(t => t.TerminalId == selectedId);
                if (selected == null)
                {
                    Trace.WriteLine("Selected terminal not found in catalog", GetType().FullName);
                    return Task.FromResult<SelectedTerminalResponse>(null);
                }

                var connection = selected.Connections?.FirstOrDefault(c => c.ConnectionId == selected.SelectedConnectionId)
                    ?? selected.Connections?.FirstOrDefault();
                if (connection == null)
                {
                    Trace.WriteLine("Selected connection not found", GetType().FullName);
                    return Task.FromResult<SelectedTerminalResponse>(null);
                }

                var response = new SelectedTerminalResponse
                {
                    Selected = new SelectedTerminal
                    {
                        TerminalId = selected.TerminalId,
                        Vendor = selected.Vendor,
                        Connection = connection,
                        SelectedAt = DateTime.UtcNow,
                        VendorPayload = selected.VendorPayload
                    }
                };
                Trace.WriteLine($"Selected terminal: {selected.TerminalId}, connection: {connection.ConnectionId}", GetType().FullName);
                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// Hash of the last committed terminal selection, used to detect whether
        /// <see cref="SelectTerminalAsync"/> needs to recreate the terminal instance.
        /// Covers terminal ID, connection ID, connection properties, loyalty flag, and vendor payloads.
        /// </summary>
        private int lastSelectionHash = 0;

        /// <summary>
        /// Computes a stable hash representing a terminal selection and its connection properties.
        /// Used by <see cref="SelectTerminalAsync"/> to skip unnecessary terminal recreation when
        /// the caller submits an identical selection.
        /// </summary>
        /// <param name="terminalId">Terminal identifier.</param>
        /// <param name="connectionId">Connection identifier.</param>
        /// <param name="connection">Full connection option, including serial, TCP, and Bluetooth details.</param>
        /// <param name="isLoyaltySupported">Whether loyalty operations are enabled for this selection.</param>
        /// <param name="terminalVendorPayload">Terminal-level vendor payload.</param>
        /// <returns>A hash that changes whenever any selection-relevant property changes.</returns>
        private int ComputeSelectionHash(string terminalId, string connectionId, TerminalConnectionOption connection, bool isLoyaltySupported, VendorPayload terminalVendorPayload)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (terminalId?.GetHashCode() ?? 0);
                hash = hash * 23 + (connectionId?.GetHashCode() ?? 0);
                hash = hash * 23 + (connection?.Serial?.PortName?.GetHashCode() ?? 0);
                hash = hash * 23 + (connection?.Tcp?.Host?.GetHashCode() ?? 0);
                hash = hash * 23 + (connection?.Tcp?.Port ?? 0);
                hash = hash * 23 + (connection?.Bluetooth?.MacAddress?.GetHashCode() ?? 0);
                hash = hash * 23 + isLoyaltySupported.GetHashCode();
                hash = hash * 23 + (connection?.VendorPayload?.GetHashCode() ?? 0);
                hash = hash * 23 + (terminalVendorPayload?.GetHashCode() ?? 0);
                return hash;
            }
        }

        private readonly SemaphoreSlim selectTerminalSemaphore = new SemaphoreSlim(1, 1);

        /// <inheritdoc/>
        public async Task<SelectedTerminalResponse> SelectTerminalAsync(SelectTerminalRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(SelectTerminalAsync)}:TerminalId={request?.TerminalId};ConnectionId={request?.Connection.ConnectionId}", GetType().FullName);

            if (request == null)
            {
                Trace.WriteLine("SelectTerminalAsync: Request cannot be null", GetType().FullName);
                throw new ApiBadRequestException("Select terminal request cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(request.TerminalId))
            {
                Trace.WriteLine("SelectTerminalAsync: TerminalId cannot be null or whitespace", GetType().FullName);
                throw new ApiBadRequestException("TerminalId cannot be null or whitespace.");
            }

            if (request.Connection == null)
            {
                Trace.WriteLine("SelectTerminalAsync: Connection cannot be null", GetType().FullName);
                throw new ApiBadRequestException("Connection cannot be null.");
            }

            await selectTerminalSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                IPaymentTerminal previousTerminal = null;
                IPaymentTerminal newTerminal = null;
                TerminalDescriptor terminal = null;
                TerminalConnectionOption catalogConnection = null;
                int hash = 0;

                lock (sync)
                {
                    terminal = terminalCatalogResponse.Terminals?.FirstOrDefault(t => t.TerminalId == request.TerminalId);
                    if (terminal == null)
                    {
                        Trace.WriteLine($"SelectTerminalAsync: Terminal {request.TerminalId} not found", GetType().FullName);
                        return null;
                    }

                    hash = ComputeSelectionHash(
                        request.TerminalId,
                        request.Connection.ConnectionId,
                        request.Connection,
                        request.IsLoyaltySupported,
                        request.VendorPayload);

                    if (lastSelectionHash == hash)
                    {
                        Trace.WriteLine("SelectTerminalAsync: Selection unchanged, skipping recreation", GetType().FullName);
                        return GetSelectedTerminalAsync(cancellationToken).Result;
                    }

                    catalogConnection = terminal.Connections?.FirstOrDefault(c => c.ConnectionId == request.Connection.ConnectionId);
                    if (catalogConnection == null)
                    {
                        var error = "No valid connection found for the selected terminal connection.";
                        Trace.WriteLine($"{nameof(SelectTerminalAsync)}: {error}", GetType().FullName);
                        throw new ApiConflictException(error);
                    }

                    terminal.IsLoyaltySupported = request.IsLoyaltySupported;
                    terminal.VendorPayload = request.VendorPayload;
                }

                newTerminal = CreateTerminalInstance(terminal, request.Connection);

                try
                {
                    await newTerminal.TestConnectionAsync();
                    await newTerminal.AbortTransactionAsync();
                }
                catch
                {
                    try
                    {
                        newTerminal.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Failed to dispose terminal after connection test failure: {ex}", GetType().FullName);
                    }

                    throw;
                }

                lock (sync)
                {
                    lastSelectionHash = hash;
                    previousTerminal = selectedPaymentTerminal;
                    selectedPaymentTerminal = newTerminal;

                    terminalCatalogResponse.SelectedTerminalId = request.TerminalId;
                    terminal.SelectedConnectionId = request.Connection.ConnectionId;

                    catalogConnection.Serial = request.Connection.Serial;
                    catalogConnection.Tcp = request.Connection.Tcp;
                    catalogConnection.Bluetooth = request.Connection.Bluetooth;
                    catalogConnection.VendorPayload = request.Connection.VendorPayload;

                    SaveCatalog(terminalCatalogResponse);

                    Trace.WriteLine("Terminal selection complete", GetType().FullName);
                }

                if (previousTerminal != null)
                {
                    try
                    {
                        await previousTerminal.ReleaseAsync();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Failed to release previous terminal: {ex}", GetType().FullName);
                    }

                    try
                    {
                        previousTerminal.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Failed to dispose previous terminal: {ex}", GetType().FullName);
                    }
                }

                return await GetSelectedTerminalAsync(cancellationToken);
            }
            finally
            {
                selectTerminalSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<OperationAccepted> ReleaseSelectedTerminalAsync()
        {
            Trace.WriteLine($"{nameof(ReleaseSelectedTerminalAsync)}", GetType().FullName);

            IPaymentTerminal terminalToRelease = null;
            lock (sync)
            {
                terminalToRelease = selectedPaymentTerminal;
                selectedPaymentTerminal = null;
                lastSelectionHash = 0;
                if (terminalCatalogResponse != null)
                {
                    terminalCatalogResponse.SelectedTerminalId = null;
                    SaveCatalog(terminalCatalogResponse);
                }
            }

            if (terminalToRelease == null)
                throw new ApiConflictException("No terminal is currently selected to release.");

            try
            {
                return await terminalToRelease.ReleaseAsync();
            }
            finally
            {
                try
                {
                    terminalToRelease.Dispose();
                }
                catch
                {
                    Trace.WriteLine($"Failed to dispose terminal: '{terminalToRelease?.TerminalId}'", GetType().FullName);
                }
            }
        }

        /// <summary>
        /// Creates a terminal instance for the selected descriptor and connection.
        /// Type: IPaymentTerminal return value.
        /// </summary>
        /// <param name="terminal">Terminal descriptor to instantiate.</param>
        /// <param name="connection">Connection information for the instance.</param>
        /// <returns>The created terminal instance.</returns>
        private IPaymentTerminal CreateTerminalInstance(TerminalDescriptor terminal, TerminalConnectionOption connection)
        {
            if (terminal == null)
                throw new ApiBadRequestException("Terminal descriptor cannot be null.");
            if (connection == null)
                throw new ApiBadRequestException("Terminal connection option cannot be null.");

            Trace.WriteLine($"{nameof(CreateTerminalInstance)}:{terminal} {connection}", GetType().FullName);

            string dirName = AppDomain.CurrentDomain.BaseDirectory;

            if (terminal.TerminalId == VerifoneYomaniXR_001)
            {
                return new VerifoneYomaniXRTerminal(
                    terminal.TerminalId,
                    terminal.DisplayName,
                    connection,
                    terminal.IsLoyaltySupported,
                    terminal.VendorPayload,
                    new FileSessionStorageProvider(Path.Combine(terminalSessionDirectoryBase, terminal.TerminalId), new TimestampFileNameProvider()));
            }
            else if (terminal.TerminalId == TestTerminal_001)
            {
                return new TestPaymentTerminal(
                    terminal.TerminalId,
                    terminal.DisplayName,
                    connection,
                    terminal.IsLoyaltySupported,
                    terminal.VendorPayload,
                    new FileSessionStorageProvider(Path.Combine(terminalSessionDirectoryBase, terminal.TerminalId), new TimestampFileNameProvider()));
            }

            throw new ApiBadRequestException($"Unknown terminal type: {terminal.TerminalId}");
        }
    }
}