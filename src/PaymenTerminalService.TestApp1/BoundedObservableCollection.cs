using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PaymentTerminalService.TestApp1
{
    public class BoundedObservableCollection<T> : ObservableCollection<T>
    {
        public enum UpdateMode
        {
            Add,     // Append at end
            Insert   // Insert at beginning (index 0)
        }

        private int _maxSize;

        public BoundedObservableCollection(
            int maxSize = 100,
            UpdateMode mode = UpdateMode.Add)
        {
            Mode = mode;
            MaxSize = maxSize;
        }

        public BoundedObservableCollection(
            int maxSize,
            IEnumerable<T> collection,
            UpdateMode mode = UpdateMode.Add)
            : base(collection)
        {
            Mode = mode;
            MaxSize = maxSize;
            TrimIfNeeded();
        }

        public UpdateMode Mode { get; }

        public int MaxSize
        {
            get { return _maxSize; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "MaxSize must be >= 0.");

                if (_maxSize == value)
                    return;

                _maxSize = value;
                TrimIfNeeded();
            }
        }

        protected override void InsertItem(int index, T item)
        {
            EnforceMode(index);

            base.InsertItem(index, item);
            TrimIfNeeded();
        }

        protected override void SetItem(int index, T item)
        {
            base.SetItem(index, item);
        }

        private void EnforceMode(int index)
        {
            if (Mode == UpdateMode.Add)
            {
                // Add() becomes InsertItem(Count, item)
                if (index != Count)
                    throw new NotSupportedException(
                        "Collection is in Add mode. Only Add() is allowed.");
            }
            else // UpdateMode.Insert
            {
                if (index != 0)
                    throw new NotSupportedException(
                        "Collection is in Insert mode. Only Insert(0, item) is allowed.");
            }
        }

        private void TrimIfNeeded()
        {
            while (Count > _maxSize)
            {
                if (Mode == UpdateMode.Add)
                {
                    // Oldest is at front
                    base.RemoveItem(0);
                }
                else
                {
                    // Oldest is at back
                    base.RemoveItem(Count - 1);
                }
            }
        }
    }
}