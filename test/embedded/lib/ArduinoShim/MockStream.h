// MockStream: a Stream implementation backed by a ring buffer.
// Feed bytes with inject(), then CmdMessenger reads them via available()/read().
// Captures output written by CmdMessenger via write().

#ifndef MOCK_STREAM_H
#define MOCK_STREAM_H

#include "Arduino.h"
#include <cstring>

#define MOCK_BUFFER_SIZE 1024

class MockStream : public Stream {
public:
    // --- Input side (simulates data arriving from "remote") ---
    uint8_t rxBuf[MOCK_BUFFER_SIZE];
    size_t rxHead = 0;
    size_t rxTail = 0;

    // --- Output side (captures what CmdMessenger sends) ---
    uint8_t txBuf[MOCK_BUFFER_SIZE];
    size_t txLen = 0;

    void inject(const char *str) {
        inject((const uint8_t *)str, strlen(str));
    }

    void inject(const uint8_t *data, size_t len) {
        for (size_t i = 0; i < len; i++) {
            rxBuf[rxHead % MOCK_BUFFER_SIZE] = data[i];
            rxHead++;
        }
    }

    // Stream interface
    int available() override {
        return (int)(rxHead - rxTail);
    }

    int read() override {
        if (rxTail >= rxHead) return -1;
        return rxBuf[rxTail++ % MOCK_BUFFER_SIZE];
    }

    int peek() override {
        if (rxTail >= rxHead) return -1;
        return rxBuf[rxTail % MOCK_BUFFER_SIZE];
    }

    // Print interface
    size_t write(uint8_t c) override {
        if (txLen < MOCK_BUFFER_SIZE) {
            txBuf[txLen++] = c;
        }
        return 1;
    }

    // Helpers
    void clearTx() { txLen = 0; }
    void clearRx() { rxHead = 0; rxTail = 0; }
    void clear() { clearTx(); clearRx(); }

    // Get output as null-terminated string
    const char *getTx() {
        txBuf[txLen < MOCK_BUFFER_SIZE ? txLen : MOCK_BUFFER_SIZE - 1] = '\0';
        return (const char *)txBuf;
    }
};

#endif // MOCK_STREAM_H
