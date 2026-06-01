// Arduino shim implementation for native builds.

#include "Arduino.h"
#include <cstdio>
#include <chrono>

static auto _startTime = std::chrono::steady_clock::now();

unsigned long millis() {
    auto now = std::chrono::steady_clock::now();
    return (unsigned long)std::chrono::duration_cast<std::chrono::milliseconds>(now - _startTime).count();
}

// --- Print ---

size_t Print::write(const uint8_t *buf, size_t size) {
    size_t n = 0;
    for (size_t i = 0; i < size; i++) {
        n += write(buf[i]);
    }
    return n;
}

size_t Print::print(const char *str) {
    if (!str) return 0;
    size_t n = 0;
    while (*str) {
        n += write((uint8_t)*str++);
    }
    return n;
}

size_t Print::print(char c) {
    return write((uint8_t)c);
}

size_t Print::print(int val, int base) {
    char buf[34];
    if (base == DEC) snprintf(buf, sizeof(buf), "%d", val);
    else if (base == HEX) snprintf(buf, sizeof(buf), "%x", val);
    else if (base == OCT) snprintf(buf, sizeof(buf), "%o", val);
    else snprintf(buf, sizeof(buf), "%d", val);
    return print(buf);
}

size_t Print::print(unsigned int val, int base) {
    char buf[34];
    if (base == DEC) snprintf(buf, sizeof(buf), "%u", val);
    else if (base == HEX) snprintf(buf, sizeof(buf), "%x", val);
    else snprintf(buf, sizeof(buf), "%u", val);
    return print(buf);
}

size_t Print::print(long val, int base) {
    char buf[34];
    if (base == DEC) snprintf(buf, sizeof(buf), "%ld", val);
    else if (base == HEX) snprintf(buf, sizeof(buf), "%lx", val);
    else snprintf(buf, sizeof(buf), "%ld", val);
    return print(buf);
}

size_t Print::print(unsigned long val, int base) {
    char buf[34];
    if (base == DEC) snprintf(buf, sizeof(buf), "%lu", val);
    else if (base == HEX) snprintf(buf, sizeof(buf), "%lx", val);
    else snprintf(buf, sizeof(buf), "%lu", val);
    return print(buf);
}

size_t Print::print(double val, int digits) {
    char buf[64];
    snprintf(buf, sizeof(buf), "%.*f", digits, val);
    return print(buf);
}

size_t Print::println() {
    return print("\r\n");
}

size_t Print::println(const char *str) {
    size_t n = print(str);
    n += println();
    return n;
}

// --- Stream ---

size_t Stream::readBytes(char *buffer, size_t length) {
    size_t count = 0;
    while (count < length) {
        int c = read();
        if (c < 0) break;
        *buffer++ = (char)c;
        count++;
    }
    return count;
}

// --- strlcpy ---

#ifndef __APPLE__  // macOS already has strlcpy
size_t strlcpy(char *dst, const char *src, size_t size) {
    size_t srclen = strlen(src);
    if (size > 0) {
        size_t copylen = (srclen >= size) ? size - 1 : srclen;
        memcpy(dst, src, copylen);
        dst[copylen] = '\0';
    }
    return srclen;
}
#endif
