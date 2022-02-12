#pragma once
#pragma message "/////////////////////////////////////"
#pragma message "// COPYRIGHT (C) Unknown6656, 2021 //"
#pragma message "//  THIS IS A HEADER-ONLY LIBRARY  //"
#pragma message "/////////////////////////////////////"

#include <vector>
#include <functional>
#include <ppl.h>


namespace unknown6656___init_block
{
    struct __empty {};

    template <class F>
    inline decltype(auto) operator+(__empty, F&& f) noexcept
    {
        return std::forward<F>(f)();
    }
}

#define init unknown6656___init_block::__empty{} + [&]() -> decltype(auto)


#define VALUE(name) __##name
#define READONLY_PROPERTY(type, name) \
    private: type VALUE(name); \
    public: inline type name() const noexcept { return VALUE(name); }
#define PROPERTY(type, name) \
    READONLY_PROPERTY(type, name); \
    public: inline void name(const type& _##name) noexcept { VALUE(name) = _##name; }

#ifndef ALLOCATOR_MAX_SLICE_SIZE
#define ALLOCATOR_MAX_SLICE_SIZE (256 * 1024 * 1024)
#endif

namespace unknown6656
{
    typedef unsigned long long ulong;

    template <typename T>
    class __property final
    {
        T value;
    public:
        virtual ~__property() = default;

        inline virtual T& operator=(const T& f)
        {
            return value = f;
        }

        inline virtual const T& operator()() const
        {
            return value;
        }

        inline virtual explicit operator const T&() const
        {
            return value;
        }

        inline virtual T* operator->()
        {
            return &value;
        }
    };

    template <typename T>
    class /*__declspec(dllexport)*/ big_fucking_allocator
    {
    public:
        static const constexpr int MAX_SLICE_SIZE = ALLOCATOR_MAX_SLICE_SIZE;

        const ulong element_count;
        const int element_size;
        const ulong binary_size = element_count * element_size;
        const int slice_count = (int)ceil((double)binary_size / MAX_SLICE_SIZE);

    private:
        const int _slicesize = MAX_SLICE_SIZE / element_size;
        T** const _slices = new T*[slice_count];

    public:
        READONLY_PROPERTY(bool, disposed)


        big_fucking_allocator(const ulong & count) noexcept
            : element_size(sizeof(T))
            , element_count(count)
            , VALUE(disposed)(false)
        {
            if (count)
                for (int slice_index = 0; slice_index < slice_count; ++slice_index)
                    _slices[slice_index] = (T*)malloc(binary_slice_size(slice_index));
        }

        big_fucking_allocator(T* const pointer, const ulong& count) noexcept
            : big_fucking_allocator(count)
        {
            if (count)
                concurrency::parallel_for(size_t(0), size_t(count), [&](size_t i) { this[i] = pointer[i]; });
        }

        big_fucking_allocator(const std::vector<T>& vector) noexcept
            : big_fucking_allocator(vector.size() ? &vector[0] : nullptr, vector.size())
        {
        }

        ~big_fucking_allocator() noexcept
        {
            destroy();
        }

        inline void destroy() noexcept
        {
            if (!VALUE(disposed))
            {
                for (int slice_index = 0; slice_index < slice_count; ++slice_index)
                    free(_slices[slice_index]);

                delete[] _slices;

                VALUE(disposed) = true;
            }
        }

        inline void clear_memory() const noexcept
        {
            for (int slice_index = 0; slice_index < slice_count; ++slice_index)
                memset(_slices[slice_index], 0, binary_slice_size(slice_index));
        }

        inline void set_memory(const T& value) const noexcept
        {
            for (int slice_index = 0; slice_index < slice_count; ++slice_index)
                concurrency::parallel_for(size_t(0), size_t(slice_size(slice_index)), [&](size_t i) { _slices[slice_index][i] = value; });
        }

        inline int slice_index(const ulong& element_index) const
        {
            if (element_index < element_count)
                return (int)(element_index / (ulong)_slicesize);
            else
                throw std::out_of_range("The element index must be smaller than the total number of elements.");
        }

        inline int slice_size(const int slice_index) const
        {
            if (slice_index < 0 || slice_index >= slice_count)
                throw std::out_of_range("The slice index must be smaller than the number of total slices and greater or equal to zero.");
            else if (slice_index < slice_count - 1)
                return _slicesize;
            else
                return (int)(element_count - (ulong)(slice_index * _slicesize));
        }

        inline int slice_size(const ulong& element_index) const
        {
            return slice_size(slice_index(element_index));
        }

        inline int binary_slice_size(int slice_index) const
        {
            return slice_size(slice_index) * element_size;
        }

        inline int binary_slice_size(const ulong& element_index) const
        {
            return slice_size(element_index) * element_size;
        }

        inline T* unsafe_slice_base_pointer(const int slice_index) const
        {
            if (slice_index >= 0 && slice_index < slice_count)
                return _slices[slice_index];
            else
                throw std::out_of_range("The slice index must be smaller than the number of total slices and greater or equal to zero.");
        }

        inline T* unsafe_slice_base_pointer(const ulong& element_index) const
        {
            return unsafe_slice_base_pointer(slice_index(element_index));
        }


        // TODO : copy to pointer
        // TODO : copy to vector
        // TODO : sub-slicing


        inline T& const operator[](const ulong & element_index) const
        {
            if (element_index < element_count)
                return _slices[element_index / _slicesize][element_index % _slicesize];
            else
                throw std::out_of_range("The element index must be smaller than the total number of elements.");
        }
    };
}
