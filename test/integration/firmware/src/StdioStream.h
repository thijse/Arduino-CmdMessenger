// StdioStream: Stream implementation using stdin/stdout pipes.
// Used for integration testing where a host driver spawns this firmware as a process.
//
// Design: a background thread does blocking fgetc() on stdin into a ring buffer.
// available()/read() consume from that buffer. This avoids the unreliability of
// PeekNamedPipe on .NET-redirected stdin pipes on Windows.

#ifndef STDIO_STREAM_H
#define STDIO_STREAM_H

// Include platform headers BEFORE Arduino.h to avoid boolean typedef conflict.
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <io.h>
#include <fcntl.h>
#endif

// Include C++ stdlib BEFORE Arduino.h so std::thread/atomic see real std::min/max
// rather than Arduino's macro-overridden versions.
#include <atomic>
#include <thread>
#include <mutex>

#include "Arduino.h"
#include <cstdio>

#define STDIO_RING_SIZE 4096

class StdioStream : public Stream {
public:
    StdioStream() {
        #ifdef _WIN32
        _setmode(_fileno(stdin), _O_BINARY);
        _setmode(_fileno(stdout), _O_BINARY);
        #endif
        head.store(0);
        tail.store(0);
        eofReached.store(false);
        std::thread reader(&StdioStream::ReaderLoop, this);
        reader.detach();
    }

    int available() override {
        size_t h = head.load(std::memory_order_acquire);
        size_t t = tail.load(std::memory_order_relaxed);
        return (int)(h - t);
    }

    int read() override {
        size_t h = head.load(std::memory_order_acquire);
        size_t t = tail.load(std::memory_order_relaxed);
        if (t >= h) return -1;
        uint8_t c = ring[t % STDIO_RING_SIZE];
        tail.store(t + 1, std::memory_order_release);
        return c;
    }

    int peek() override {
        size_t h = head.load(std::memory_order_acquire);
        size_t t = tail.load(std::memory_order_relaxed);
        if (t >= h) return -1;
        return ring[t % STDIO_RING_SIZE];
    }

    size_t write(uint8_t c) override {
        std::lock_guard<std::mutex> lock(writeMutex);
        fputc(c, stdout);
        fflush(stdout);
        return 1;
    }

    using Print::write; // inherit write(buf, size)

    bool isEof() const { return eofReached.load(); }

private:
    uint8_t ring[STDIO_RING_SIZE];
    std::atomic<size_t> head;
    std::atomic<size_t> tail;
    std::atomic<bool> eofReached;
    std::mutex writeMutex;

    void ReaderLoop() {
        while (true) {
            int c = fgetc(stdin); // blocks until byte arrives or EOF
            if (c == EOF) {
                eofReached.store(true);
                return;
            }
            // Spin-wait if buffer is full
            while (true) {
                size_t h = head.load(std::memory_order_relaxed);
                size_t t = tail.load(std::memory_order_acquire);
                if (h - t < STDIO_RING_SIZE) {
                    ring[h % STDIO_RING_SIZE] = (uint8_t)c;
                    head.store(h + 1, std::memory_order_release);
                    break;
                }
                std::this_thread::yield();
            }
        }
    }
};

#endif // STDIO_STREAM_H
