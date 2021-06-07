using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Unknown6656.BigFuckingAllocator
{
    /// <summary>
    /// Represents an abstract memory allocator which uses a set of non-continuous memory slices.
    /// </summary>
    public unsafe interface IBigFuckingAllocator
        : IDisposable
    {
        /// <summary>
        /// The maximum binary size of a memory slice.
        /// The default maximum size are 256 MB.
        /// </summary>
        public const int MaximumSliceSize = 256 * 1024 * 1024;

        /// <summary>
        /// Returns the binary size of a single element (of type <see cref="T"/>).
        /// </summary>
        int ElementSize { get; }
        /// <summary>
        /// Returns the <see cref="Type"/> of <see cref="T"/>.
        /// </summary>
        Type ElementType { get; }
        /// <summary>
        /// Indicates whether the memory allocator has been disposed and all its underlying resources are cleared.
        /// </summary>
        bool IsDisposed { get; }
        /// <summary>
        /// Returns the number of items in the allocated memory region.
        /// </summary>
        ulong ItemCount { get; }
        /// <summary>
        /// Returns the number of bytes in the allocated memory region.
        /// This value <i>usually</i> corresponds to <see cref="ItemCount"/> multiplied with the size of a single item.
        /// </summary>
        ulong BinarySize { get; }
        /// <summary>
        /// Returns the number of allocated memory slices.
        /// The size of each memory slice can be obtained using <see cref="UnsafeGetSliceSize(int)"/>.
        /// </summary>
        int SliceCount { get; }

        /// <summary>
        /// Returns a read/write-pointer to the element at the given index. The pointer is guaranteed to remain the same during the allocator's lifespan.
        /// <para/>
        /// <b>WARNING: Due to the non-continuous layout of the allocated memory slices, two sequential index accesses are NOT guaranteed to return sequential pointers.</b>
        /// </summary>
        /// <param name="index">Element index which must be positive and smaller than <see cref="ItemCount"/>.</param>
        /// <returns>Memory pointer with read and write access.</returns>
        void* this[ulong element_index] { get; }

        /// <summary>
        /// Clears the allocated memory by filling it with 0-bytes.
        /// <para/>
        /// This does <b>NOT</b> free the allocated memory.
        /// Use the <see cref="IDisposable.Dispose"/>-method for that purpose.
        /// </summary>
        void MemoryClear();
        /// <summary>
        /// The the index of the slice in which the given element index is residing.
        /// This is generally computed by an integer division of <paramref name="element_index"/> and the number of elements per slice.
        /// </summary>
        /// <param name="element_index">The element index.</param>
        /// <returns>The corresponding slice index.</returns>
        int GetSliceIndex(ulong element_index);
        /// <summary>
        /// Returns the size of the slice associated with the given slice index.
        /// </summary>
        /// <param name="slice_index">The slice's index.</param>
        /// <returns>The slice's size.</returns>
        int GetSliceSize(int slice_index);
        /// <summary>
        /// Returns the size of the slice associated with the given element index.
        /// </summary>
        /// <param name="slice_index">The element index.</param>
        /// <returns>The associated slice's size.</returns>
        int GetSliceSize(ulong element_index);
        /// <summary>
        /// Returns the <b>binary</b> size of the slice associated with the given slice index.
        /// </summary>
        /// <param name="slice_index">The slice's index.</param>
        /// <returns>The slice's <b>binary</b> size.</returns>
        int GetBinarySliceSize(int slice_index) => GetSliceSize(slice_index) * ElementSize;
        /// <summary>
        /// Returns the <b>binary</b> size of the slice associated with the given element index.
        /// </summary>
        /// <param name="slice_index">The element index.</param>
        /// <returns>The associated slice's <b>binary</b> size.</returns>
        int GetBinarySliceSize(ulong element_index) => GetSliceSize(element_index) * ElementSize;
    }

    /// <summary>
    /// Represents a memory allocator for the generic native element type <typeparamref name="T"/> which uses a set of non-continuous memory slices.
    /// <br/>
    /// Use <see cref="BigFuckingAllocator"/> for an allocator for the native element type <see cref="byte"/>.
    /// <para/>
    /// <b>NOTE:</b> The native size of <typeparamref name="T"/> must not exceed <see cref="IBigFuckingAllocator.MaximumSliceSize"/>.
    /// </summary>
    /// <typeparam name="T">The generic (native) element type.</typeparam>
    public unsafe class BigFuckingAllocator<T>
        : IBigFuckingAllocator
        where T : unmanaged
    {
        /// <summary>
        /// The number of allocated memory slices.
        /// </summary>
        private readonly int _slicecount;
        /// <summary>
        /// The number of <see cref="T"/>-items per slice.
        /// </summary>
        private readonly int _slicesize;
        /// <summary>
        /// The individual slices.
        /// </summary>
        private readonly T*[] _slices;


        public bool IsDisposed { get; private set; }

        public int ElementSize => sizeof(T);

        public Type ElementType => typeof(T);

        public ulong ItemCount { get; }

        public ulong BinarySize => ItemCount * (ulong)sizeof(T);

        public int SliceCount => _slices.Length;

        void* IBigFuckingAllocator.this[ulong element_index] => this[element_index];

        /// <inheritdoc cref="IBigFuckingAllocator.this{ulong}"/>
        public T* this[ulong element_index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => element_index < ItemCount ? _slices[element_index / (ulong)_slicesize] + (element_index % (ulong)_slicesize) : throw new ArgumentOutOfRangeException(nameof(element_index), element_index, $"The index must be smaller than {ItemCount}.");
        }

        /// <inheritdoc cref="Range(Index, Index)"/>
        public BigFuckingAllocator<T> this[Index start, Index end]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Range(start, end);
        }

        /// <inheritdoc cref="Range(Range)"/>
        public BigFuckingAllocator<T> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Range(range);
        }


        static BigFuckingAllocator()
        {
            if (sizeof(T) > IBigFuckingAllocator.MaximumSliceSize)
                throw new ArgumentException($"The generic parameter type '{typeof(T)}' cannot be used, as it exceeds the {IBigFuckingAllocator.MaximumSliceSize} byte limit.", nameof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ~BigFuckingAllocator() => Dispose(managed_call: false);

        /// <summary>
        /// Creates a new (non-destructive) allocator for the given collection.
        /// </summary>
        /// <param name="collection">Collection of items.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(IEnumerable<T> collection)
            : this(collection as T[] ?? collection.ToArray())
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(Span<T> memory)
            : this(memory.ToArray())
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(Memory<T> memory)
            : this(memory.ToArray())
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(ReadOnlySpan<T> memory)
            : this(memory.ToArray())
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(ReadOnlyMemory<T> memory)
            : this(memory.ToArray())
        {
        }

        /// <summary>
        /// Creates a new (non-destructive) allocator for the given array.
        /// </summary>
        /// <param name="collection">Array of items.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(params T[] array)
            : this((ulong)array.LongLength) => Parallel.For(0, array.LongLength, i => *this[(ulong)i] = array[i]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(T* pointer, ulong count)
            : this(count) => Parallel.For(0, (long)count, i => *this[(ulong)i] = pointer[i]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator(ulong item_count)
        {
            ItemCount = item_count;
            _slicesize = IBigFuckingAllocator.MaximumSliceSize / ElementSize;
            _slicecount = (int)Math.Ceiling((double)item_count / _slicesize);
            _slices = new T*[_slicecount];

            for (int slice_index = 0; slice_index < _slicecount; ++slice_index)
            {
                int count = slice_index < _slicecount - 1 ? _slicesize : (int)(item_count - (ulong)(slice_index * _slicesize));

                _slices[slice_index] = (T*)Marshal.AllocHGlobal(count * ElementSize);
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
        public void MemoryClear() => MemorySet(default);

        /// <summary>
        /// Sets all elements of the allocated memory to the given value.
        /// </summary>
        /// <param name="value">Value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MemorySet(T value)
        {
            for (int i = 0; i < _slicecount; ++i)
            {
                T* ptr = _slices[i];
                byte* bptr = (byte*)ptr;
                int length = i < _slices.Length - 1 ? _slicesize : (int)(ItemCount - (ulong)i * (ulong)_slicesize);

                Parallel.For(0, length, j => ptr[j] = value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public BigFuckingAllocator<T> Range(Range range)
        {
            (int offs, int len) = range.GetOffsetAndLength((int)ItemCount);
            T[] segment = new T[len];

            Parallel.For(0, offs, i => segment[i] = *this[(ulong)offs + (ulong)i]);

            return new(segment);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigFuckingAllocator<T> Range(Index start, Index end) => Range(start..end);

        /// <summary>
        /// Returns the slice associated with the given slice index.
        /// </summary>
        /// <param name="slice_index">Slice index.</param>
        /// <returns>Memory slice.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetSlice(int slice_index) => new(_slices[slice_index], GetSliceSize(slice_index));

        /// <summary>
        /// Returns the slice associated with the given element index.
        /// </summary>
        /// <param name="element_index">Element index.</param>
        /// <returns>Memory slice.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetSlice(ulong element_index) => GetSlice(GetSliceIndex(element_index));

        public int GetSliceIndex(ulong element_index) => element_index < ItemCount ? (int)(element_index / (ulong)_slicesize) : throw new ArgumentOutOfRangeException(nameof(element_index), element_index, $"The element index must be smaller than {ItemCount}.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSliceSize(int slice_index)
        {
            if (slice_index < 0 || slice_index >= _slicecount)
                throw new ArgumentOutOfRangeException(nameof(slice_index), slice_index, $"The slice index must be smaller than {_slicecount} and greater or equal to zero.");
            else if (slice_index < _slicecount - 1)
                return _slicesize;
            else
                return (int)(ItemCount - (ulong)(slice_index * _slicesize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSliceSize(ulong element_index) => GetSliceSize(GetSliceIndex(element_index));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBinarySliceSize(int slice_index) => GetSliceSize(slice_index) * ElementSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBinarySliceSize(ulong element_index) => GetSliceSize(element_index) * ElementSize;

        /// <summary>
        /// <b>UNSAFE!</b>
        /// <br/>
        /// Returns a pointer with read/write access which points to the start of the slice associated with the given slice index.
        /// </summary>
        /// <param name="slice_index">Slice index.</param>
        /// <returns>Slice pointer.</returns>
        public T* UnsafeGetSliceBasePointer(int slice_index) =>
            slice_index >= 0 && slice_index < _slicecount ? _slices[slice_index] : throw new ArgumentOutOfRangeException(nameof(slice_index), slice_index, $"The slice index must be smaller than {_slicecount} and greater or equal to zero.");

        /// <summary>
        /// <b>UNSAFE!</b>
        /// <br/>
        /// Returns a pointer with read/write access which points to the start of the slice associated with the given element index.
        /// </summary>
        /// <param name="element_index">Element index.</param>
        /// <returns>Slice pointer.</returns>
        public T* UnsafeGetSliceBasePointer(ulong element_index) => UnsafeGetSliceBasePointer(GetSliceIndex(element_index));

        public IEnumerable<T> AsIEnumerable()
        {
            // copy local fields to prevent future binding
            ulong sz = (ulong)_slicecount;
            ulong c = ItemCount;
            T*[] sl = _slices;

            // 'UNSAFE CODE MAY NOT APPEAR IN ITERATORS'
            IEnumerable<T> iterator(Func<ulong, T> func)
            {
                for (ulong i = 0; i < c; ++i)
                    yield return func(i);
            }

            return iterator(i => sl[i / sz][i % sz]);
        }

        public T[] ToArray()
        {
        }

        public Span<T> ToSpan()
        {
        }

        public ReadOnlySpan<T> ToReadonlySpan()
        {
        }

        public Memory<T> ToMemory()
        {
        }

        public ReadOnlyMemory<T> ToReadonlyMemory()
        {
        }


        // TODO : merge multiple allocators
        // TODO : cast allocator (?)


        public static implicit operator BigFuckingAllocator<T>(T[] array) => new(array);

        public static implicit operator BigFuckingAllocator<T>(Span<T> span) => new(span);

        public static implicit operator BigFuckingAllocator<T>(ReadOnlySpan<T> span) => new(span);

        public static implicit operator BigFuckingAllocator<T>(Memory<T> memory) => new(memory.Span);

        public static implicit operator BigFuckingAllocator<T>(ReadOnlyMemory<T> memory) => new(memory.Span);


        public static explicit operator T[](BigFuckingAllocator<T> allocator) => allocator.ToArray();

        public static explicit operator Span<T>(BigFuckingAllocator<T> allocator) => allocator.ToSpan();

        public static explicit operator ReadOnlySpan<T>(BigFuckingAllocator<T> allocator) => allocator.ToReadonlySpan();

        public static explicit operator Memory<T>(BigFuckingAllocator<T> allocator) => allocator.ToMemory();

        public static explicit operator ReadOnlyMemory<T>(BigFuckingAllocator<T> allocator) => allocator.ToReadonlyMemory();
    }

    public unsafe class BigFuckingAllocator
        : BigFuckingAllocator<byte>
    {
        public BigFuckingAllocator(IEnumerable<byte> collection)
            : base(collection)
        {
        }

        public BigFuckingAllocator(params byte[] array)
            : base(array)
        {
        }

        public BigFuckingAllocator(Span<byte> memory)
            : base(memory)
        {
        }

        public BigFuckingAllocator(ulong item_count)
            : base(item_count)
        {
        }

        public BigFuckingAllocator(void* pointer, long count)
            : base((byte*)pointer, count)
        {
        }
    }
}
