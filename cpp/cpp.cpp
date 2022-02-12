#include <iostream>
#include "big_fucking_allocator.hpp"

using namespace unknown6656;


int main(const int, const char**)
{
    big_fucking_allocator<unsigned char> allocator(1UL * 1024UL * 1024UL * 1024UL);

    allocator.set_memory('ÿ');

    system("pause");

    allocator.destroy();

    return 0;
}
