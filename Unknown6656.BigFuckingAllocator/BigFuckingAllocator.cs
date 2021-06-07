using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Unknown6656.BigFuckingAllocator
{
    public unsafe sealed class BigFuckingAllocator<T>
        : IDisposable
        where T : unmanaged
    {
        public const int MAX_SLICE_SIZE = 256 * 1024 * 1024;

        private readonly int _slicecount;
        private readonly int _slicesize;
        private readonly T*[] _slices;


        public bool IsDisposed { get; private set; }

        public static int ElementSize { get; }

        public static Type ElementType { get; }

        public ulong ItemCount { get; }

        public ulong BinarySize => ItemCount * (ulong)sizeof(T);

        public int SliceCount => _slices.Length;

        public T* this[ulong idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => idx < ItemCount ? _slices[idx / (ulong)_slicesize] + (idx % (ulong)_slicesize) : throw new ArgumentOutOfRangeException(nameof(idx), idx, $"The index must be smaller than {ItemCount}.");
        }

        public Span<T> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                (int offs, int len) = range.GetOffsetAndLength((int)ItemCount);

                Span<T> span = new T[len];

                for (int i = 0; i < offs; ++i)
                    span[i] = *this[(ulong)offs + (ulong)i];

                return span;
            }
        }


        static BigFuckingAllocator()
        {
            ElementType = typeof(T);
            ElementSize = sizeof(T);

            if (ElementSize > MAX_SLICE_SIZE)
                throw new ArgumentException($"The generic parameter type '{ElementType}' cannot be used, as it exceeds the {MAX_SLICE_SIZE} byte limit.", nameof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ~BigFuckingAllocator() => Dispose(managed_call: false);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(T[] array)
            : this((ulong)array.LongLength)
        {
            for (long i = 0; i < array.LongLength; ++i)
                *this[(ulong)i] = array[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(Span<T> memory)
            : this(memory.ToArray())
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(T* pointer, int count)
        {
            ItemCount = (ulong)count;
            _slicesize = count;
            _slicecount = 0;
            _slices = new[] { pointer };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(ulong item_count)
        {
            ItemCount = item_count;
            _slicesize = MAX_SLICE_SIZE / sizeof(T);
            _slicecount = (int)Math.Ceiling((double)item_count / _slicesize);
            _slices = new T*[_slicecount];

            for (int i = 0; i < _slicecount; ++i)
            {
                int count = i < _slicecount - 1 ? _slicesize : (int)(item_count - (ulong)(i * _slicesize));

                _slices[i] = (T*)Marshal.AllocHGlobal(count * sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            Dispose(managed_call: true);
        }

        private void Dispose(bool managed_call)
        {
            if (!IsDisposed)
            {
                if (managed_call)
                    ; // TODO : managed disposals

                for (int i = 0; i < _slicecount; ++i)
                    if (_slices[i] != null)
                        try
                        {
                            Marshal.FreeHGlobal((nint)_slices[i]);
                        }
                        catch
                        {
                            _slices[i] = null;
                        }

                IsDisposed = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AggressivelyClaimAllTheFuckingMemory(T value = default)
        {
            for (int i = 0; i < _slicecount; ++i)
            {
                T* ptr = _slices[i];
                byte* bptr = (byte*)ptr;
                int length = i < _slices.Length - 1 ? _slicesize : (int)(ItemCount - (ulong)i * (ulong)_slicesize);

                Parallel.For(0, length, j => ptr[j] = value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetSlice(int index) => new(_slices[index], index < _slices.Length - 1 ? _slicesize : (int)(ItemCount - (ulong)index * (ulong)_slicesize));

        public IEnumerable<T> AsIEnumerable()
        {
            // copy local fields to prevent future binding
            ulong sz = (ulong)_slicecount;
            ulong c = ItemCount;
            T*[] sl = _slices;

            IEnumerable<T> iterator(Func<ulong, T> func)
            {
                for (ulong i = 0; i < c; ++i)
                    yield return func(i);
            }

            return iterator(i => sl[i / sz][i % sz]);
        }

        public static implicit operator BigFuckingAllocator<T>(T[] array) => new(array);

        public static implicit operator BigFuckingAllocator<T>(Span<T> span) => new(span);

        public static implicit operator BigFuckingAllocator<T>(Memory<T> memory) => new(memory.Span);
    }
}
