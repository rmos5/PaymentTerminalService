using System;
using System.Collections.Generic;

namespace PaymentTerminalService.Model
{
    /// <inheritdoc/>
    public partial class VendorPayload
    {
        /// <summary>
        /// Determines whether this instance is equal to another <see cref="VendorPayload"/>.
        /// </summary>
        /// <param name="other">Other vendor payload.</param>
        /// <returns>True if equal; otherwise false.</returns>
        public bool Equals(VendorPayload other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return DictionaryEquality.DictionaryEquals(AdditionalProperties, other.AdditionalProperties);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as VendorPayload);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return DictionaryEquality.GetDictionaryHashCode(AdditionalProperties);
            }
        }
    }

    /// <inheritdoc/>
    public partial class FaultInfo
    {
        /// <summary>
        /// Determines whether this instance is equal to another <see cref="FaultInfo"/>.
        /// </summary>
        /// <param name="other">Other fault info.</param>
        /// <returns>True if equal; otherwise false.</returns>
        public bool Equals(FaultInfo other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(Code, other.Code, StringComparison.Ordinal)
                && string.Equals(Message, other.Message, StringComparison.Ordinal)
                && Equals(VendorPayload, other.VendorPayload)
                && DictionaryEquality.DictionaryEquals(AdditionalProperties, other.AdditionalProperties);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as FaultInfo);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(Code ?? string.Empty);
                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(Message ?? string.Empty);
                hash = (hash * 23) + (VendorPayload?.GetHashCode() ?? 0);
                hash = (hash * 23) + DictionaryEquality.GetDictionaryHashCode(AdditionalProperties);

                return hash;
            }
        }
    }

    /// <inheritdoc/>
    public partial class OperationResult
    {
        /// <summary>
        /// Determines whether this instance is equal to another <see cref="OperationResult"/>.
        /// </summary>
        /// <param name="other">Other operation result.</param>
        /// <returns>True if equal; otherwise false.</returns>
        public bool Equals(OperationResult other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return Equals(VendorPayload, other.VendorPayload)
                && DictionaryEquality.DictionaryEquals(AdditionalProperties, other.AdditionalProperties);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as OperationResult);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                hash = (hash * 23) + (VendorPayload?.GetHashCode() ?? 0);
                hash = (hash * 23) + DictionaryEquality.GetDictionaryHashCode(AdditionalProperties);

                return hash;
            }
        }
    }

    /// <inheritdoc/>
    public partial class PromptInputSpec
    {
        /// <summary>
        /// Determines whether this instance is equal to another <see cref="PromptInputSpec"/>.
        /// </summary>
        /// <param name="other">Other prompt input spec.</param>
        /// <returns>True if equal; otherwise false.</returns>
        public bool Equals(PromptInputSpec other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return Kind == other.Kind
                && MinLength == other.MinLength
                && MaxLength == other.MaxLength
                && string.Equals(Hint, other.Hint, StringComparison.Ordinal)
                && DictionaryEquality.DictionaryEquals(AdditionalProperties, other.AdditionalProperties);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as PromptInputSpec);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                hash = (hash * 23) + Kind.GetHashCode();
                hash = (hash * 23) + MinLength.GetHashCode();
                hash = (hash * 23) + MaxLength.GetHashCode();
                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(Hint ?? string.Empty);
                hash = (hash * 23) + DictionaryEquality.GetDictionaryHashCode(AdditionalProperties);

                return hash;
            }
        }
    }

    /// <inheritdoc/>
    public partial class Prompt
    {
        /// <summary>
        /// Determines whether this instance is equal to another <see cref="Prompt"/>.
        /// </summary>
        /// <param name="other">Other prompt.</param>
        /// <returns>True if equal; otherwise false.</returns>
        public bool Equals(Prompt other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(PromptId, other.PromptId, StringComparison.Ordinal)
                && string.Equals(Message, other.Message, StringComparison.Ordinal)
                && YesNo == other.YesNo
                && Equals(Input, other.Input)
                && CreatedAt.Equals(other.CreatedAt)
                && ExpiresAt.Equals(other.ExpiresAt)
                && Equals(VendorPayload, other.VendorPayload)
                && DictionaryEquality.DictionaryEquals(AdditionalProperties, other.AdditionalProperties);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as Prompt);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(PromptId ?? string.Empty);
                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(Message ?? string.Empty);
                hash = (hash * 23) + YesNo.GetHashCode();
                hash = (hash * 23) + (Input?.GetHashCode() ?? 0);
                hash = (hash * 23) + CreatedAt.GetHashCode();
                hash = (hash * 23) + ExpiresAt.GetHashCode();
                hash = (hash * 23) + (VendorPayload?.GetHashCode() ?? 0);
                hash = (hash * 23) + DictionaryEquality.GetDictionaryHashCode(AdditionalProperties);

                return hash;
            }
        }
    }

    /// <inheritdoc/>
    public partial class TerminalStatus
    {
        /// <summary>
        /// Returns a human-readable summary of the terminal status.
        /// The summary includes state, connection status, result finality, status message, active operation type, operation ID, and fault details if present.
        /// Each element is separated by " | ".
        /// </summary>
        /// <returns>
        /// A concise string summarizing the terminal status, including key operational and diagnostic details.
        /// </returns>
        public override string ToString()
        {
            var parts = new List<string>
            {
                $"State={State}",
                $"Connected={IsConnected}",
                $"Final={LastResultIsFinal}"
            };

            if (!string.IsNullOrWhiteSpace(Message))
                parts.Add($"Msg=\"{Message}\"");

            if (ActiveOperationType != OperationType.None)
                parts.Add($"Op={ActiveOperationType}");

            if (!string.IsNullOrWhiteSpace(ActiveOperationId))
                parts.Add($"OpId={ActiveOperationId}");

            if (Fault != null)
                parts.Add($"Fault={Fault.Code}: {Fault.Message}");

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Determines whether this instance is equal to another <see cref="TerminalStatus"/>.
        /// </summary>
        /// <param name="other">Other terminal status.</param>
        /// <returns>True if equal; otherwise false.</returns>
        public bool Equals(TerminalStatus other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return State == other.State
                && PreviousState == other.PreviousState
                && string.Equals(Message, other.Message, StringComparison.Ordinal)
                && IsConnected == other.IsConnected
                && LastResultIsFinal == other.LastResultIsFinal
                && string.Equals(ActiveOperationId, other.ActiveOperationId, StringComparison.Ordinal)
                && ActiveOperationType == other.ActiveOperationType
                && string.Equals(ClientReference, other.ClientReference, StringComparison.Ordinal)
                && Equals(Prompt, other.Prompt)
                && Equals(OperationResult, other.OperationResult)
                && Equals(Fault, other.Fault)
                && UpdatedAt.Equals(other.UpdatedAt)
                && DictionaryEquality.DictionaryEquals(AdditionalProperties, other.AdditionalProperties);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as TerminalStatus);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                hash = (hash * 23) + State.GetHashCode();
                hash = (hash * 23) + PreviousState.GetHashCode();
                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(Message ?? string.Empty);
                hash = (hash * 23) + IsConnected.GetHashCode();
                hash = (hash * 23) + LastResultIsFinal.GetHashCode();
                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(ActiveOperationId ?? string.Empty);
                hash = (hash * 23) + ActiveOperationType.GetHashCode();
                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(ClientReference ?? string.Empty);
                hash = (hash * 23) + (Prompt?.GetHashCode() ?? 0);
                hash = (hash * 23) + (OperationResult?.GetHashCode() ?? 0);
                hash = (hash * 23) + (Fault?.GetHashCode() ?? 0);
                hash = (hash * 23) + UpdatedAt.GetHashCode();
                hash = (hash * 23) + DictionaryEquality.GetDictionaryHashCode(AdditionalProperties);

                return hash;
            }
        }
    }

    /// <inheritdoc/>
    public partial class PurchaseRequest
    {
        /// <summary>
        /// Returns a concise, human-readable summary of the purchase request.
        /// Includes amount, currency, loyalty handling, client reference, and vendor payload presence.
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>
            {
                $"Amount={Amount}",
                $"Currency={Currency ?? "N/A"}",
                $"LoyaltyHandled={IsLoyaltyHandled}"
            };

            if (!string.IsNullOrWhiteSpace(ClientReference))
                parts.Add($"ClientRef={ClientReference}");

            if (VendorPayload != null)
                parts.Add("VendorPayload=Yes");

            return string.Join(" | ", parts);
        }
    }

    /// <inheritdoc/>
    public partial class ReversalRequest
    {
        /// <summary>
        /// Returns a concise, human-readable summary of the reversal request.
        /// Includes transaction id, timestamp, client reference, and vendor payload presence.
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>
            {
                $"TransactionId={TransactionId ?? "N/A"}",
                $"Timestamp={Timestamp:O}",
            };

            if (!string.IsNullOrWhiteSpace(ClientReference))
                parts.Add($"ClientRef={ClientReference}");

            if (VendorPayload != null)
                parts.Add("VendorPayload=Yes");

            return string.Join(" | ", parts);
        }
    }

    /// <inheritdoc/>
    public partial class RefundRequest
    {
        /// <summary>
        /// Returns a concise, human-readable summary of the refund request.
        /// Includes amount, currency, client reference, and vendor payload presence.
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>
            {
                $"Amount={Amount}",
                $"Currency={Currency ?? "N/A"}",
            };

            if (!string.IsNullOrWhiteSpace(ClientReference))
                parts.Add($"ClientRef={ClientReference}");

            if (VendorPayload != null)
                parts.Add("VendorPayload=Yes");

            return string.Join(" | ", parts);
        }
    }

    /// <inheritdoc/>
    public partial class TerminalSessionResponse
    {
        /// <summary>
        /// Returns a concise summary for Visual Studio debugging, including session name, status count,
        /// and the latest status (if available).
        /// </summary>
        /// <returns>Debug-friendly session summary string.</returns>
        public override string ToString()
        {
            var statusCount = Statuses?.Count ?? 0;
            TerminalStatus latestStatus = null;

            if (Statuses != null)
            {
                foreach (var status in Statuses)
                {
                    latestStatus = status;
                    break;
                }
            }

            var sessionName = string.IsNullOrWhiteSpace(SessionName) ? "N/A" : SessionName;
            var latestStatusText = latestStatus == null ? "N/A" : latestStatus.ToString();

            return $"SessionName={sessionName} | StatusCount={statusCount} | LatestStatus={latestStatusText}";
        }

        /// <summary>
        /// Determines whether this instance is equal to another <see cref="TerminalSessionResponse"/>.
        /// </summary>
        /// <param name="other">Other terminal session response.</param>
        /// <returns>True if equal; otherwise false.</returns>
        public bool Equals(TerminalSessionResponse other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(SessionName, other.SessionName, StringComparison.Ordinal)
                && StatusesEqual(Statuses, other.Statuses)
                && DictionaryEquality.DictionaryEquals(AdditionalProperties, other.AdditionalProperties);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Equals(obj as TerminalSessionResponse);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                hash = (hash * 23) + StringComparer.Ordinal.GetHashCode(SessionName ?? string.Empty);
                hash = (hash * 23) + GetStatusesHashCode(Statuses);
                hash = (hash * 23) + DictionaryEquality.GetDictionaryHashCode(AdditionalProperties);

                return hash;
            }
        }

        private static bool StatusesEqual(ICollection<TerminalStatus> source, ICollection<TerminalStatus> other)
        {
            if (ReferenceEquals(source, other))
                return true;
            if (source == null || other == null)
                return false;
            if (source.Count != other.Count)
                return false;

            using (var sourceEnumerator = source.GetEnumerator())
            using (var otherEnumerator = other.GetEnumerator())
            {
                while (true)
                {
                    var sourceNext = sourceEnumerator.MoveNext();
                    var otherNext = otherEnumerator.MoveNext();

                    if (!sourceNext || !otherNext)
                        return sourceNext == otherNext;

                    if (!Equals(sourceEnumerator.Current, otherEnumerator.Current))
                        return false;
                }
            }
        }

        private static int GetStatusesHashCode(ICollection<TerminalStatus> statuses)
        {
            if (statuses == null || statuses.Count == 0)
                return 0;

            unchecked
            {
                var hash = 17;

                foreach (var status in statuses)
                {
                    hash = (hash * 23) + (status?.GetHashCode() ?? 0);
                }

                return hash;
            }
        }
    }
}
