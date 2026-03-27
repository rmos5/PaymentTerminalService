using System;
using System.Collections.Generic;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Provides equality and hash code helpers for collections and dictionaries.
    /// </summary>
    public static class CollectionEquality
    {
        /// <summary>
        /// Compares two dictionaries for equality by key/value pairs, ignoring order.
        /// </summary>
        /// <param name="source">Source dictionary.</param>
        /// <param name="other">Other dictionary.</param>
        /// <returns>True when the dictionaries contain the same entries; otherwise false.</returns>
        public static bool DictionaryEquals(
            this IDictionary<string, object> source,
            IDictionary<string, object> other)
        {
            if (ReferenceEquals(source, other))
                return true;
            if (source == null || other == null)
                return false;
            if (source.Count != other.Count)
                return false;

            foreach (var item in source)
            {
                if (!other.TryGetValue(item.Key, out var otherValue))
                    return false;

                if (!Equals(item.Value, otherValue))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Computes an order-independent hash code for a dictionary.
        /// </summary>
        /// <param name="dictionary">Dictionary instance.</param>
        /// <returns>Combined hash code for the dictionary entries.</returns>
        public static int GetDictionaryHashCode(IDictionary<string, object> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
                return 0;

            unchecked
            {
                var hash = 0;

                foreach (var item in dictionary)
                {
                    var keyHash = StringComparer.Ordinal.GetHashCode(item.Key ?? string.Empty);
                    var valueHash = item.Value?.GetHashCode() ?? 0;
                    hash ^= (keyHash * 397) ^ valueHash;
                }

                return hash;
            }
        }

        /// <summary>
        /// Compares two collections for sequential equality using each element's
        /// <see cref="object.Equals(object)"/> override.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">Source collection.</param>
        /// <param name="other">Other collection.</param>
        /// <returns>True when both collections contain equal elements in the same order; otherwise false.</returns>
        public static bool CollectionEquals<T>(
            this ICollection<T> source,
            ICollection<T> other)
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

        /// <summary>
        /// Computes an order-dependent hash code for a collection using each element's
        /// <see cref="object.GetHashCode"/> override.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="collection">Collection instance.</param>
        /// <returns>Combined hash code for the collection elements.</returns>
        public static int GetCollectionHashCode<T>(ICollection<T> collection)
        {
            if (collection == null || collection.Count == 0)
                return 0;

            unchecked
            {
                var hash = 17;

                foreach (var item in collection)
                    hash = (hash * 23) + (item?.GetHashCode() ?? 0);

                return hash;
            }
        }
    }

    /// <summary>
    /// Provides typed access helpers for dictionaries that store object values.
    /// </summary>
    public static class DictionaryPropertyExtensions
    {
        /// <summary>
        /// Attempts to get a property by name.
        /// Returns false when the property does not exist.
        /// Throws when the property exists but cannot be converted to the requested type.
        /// </summary>
        /// <typeparam name="T">Requested property type.</typeparam>
        /// <param name="source">Source dictionary.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="propertyValue">Resolved property value.</param>
        /// <returns>True when the property exists; otherwise false.</returns>
        public static bool TryGetProperty<T>(this IDictionary<string, object> source, string propertyName, out T propertyValue)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Property name must not be null or whitespace.", nameof(propertyName));

            if (!source.TryGetValue(propertyName, out var propertyObject))
            {
                propertyValue = default;
                return false;
            }

            if (propertyObject == null)
            {
                if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                {
                    throw new InvalidOperationException(
                        $"Property '{propertyName}' is null and cannot be converted to {typeof(T).FullName}.");
                }

                propertyValue = default(T);
                return true;
            }

            if (propertyObject is T typedValue)
            {
                propertyValue = typedValue;
                return true;
            }

            var token = propertyObject as Newtonsoft.Json.Linq.JToken;
            if (token != null)
            {
                try
                {
                    propertyValue = token.ToObject<T>();
                    return true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Property '{propertyName}' cannot be converted to {typeof(T).FullName}.",
                        ex);
                }
            }

            throw new InvalidOperationException(
                $"Property '{propertyName}' is of type {propertyObject.GetType().FullName} and cannot be converted to {typeof(T).FullName}.");
        }

        /// <summary>
        /// Gets a property value or returns the supplied default when the property does not exist.
        /// </summary>
        /// <typeparam name="T">Requested property type.</typeparam>
        /// <param name="source">Source dictionary.</param>
        /// <param name="propertyName">Property name.</param>
        /// <param name="defaultValue">Default value used when the property does not exist.</param>
        /// <returns>The resolved property value or <paramref name="defaultValue"/>.</returns>
        public static T GetPropertyOrDefault<T>(this IDictionary<string, object> source, string propertyName, T defaultValue = default(T))
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (source.TryGetProperty(propertyName, out T propertyValue))
                return propertyValue;

            return defaultValue;
        }
    }
}
