// Minimal Arduino.h shim for native (host) builds of CmdMessenger tests.
// Provides just enough of the Arduino API to compile CmdMessenger.cpp.

#ifndef ARDUINO_H_SHIM
#define ARDUINO_H_SHIM

#include <cstdint>
#include <cstddef>
#include <cstring>
#include <cstdlib>
#include <cmath>
#include <algorithm>

// Arduino types
typedef uint8_t byte;
#if !defined(_RPCNDR_H) && !defined(__RPCNDR_H__)
typedef bool boolean;
#endif

// Expose C99 math macros for CmdMessenger.cpp (isinf, isnan)
using std::isinf;
using std::isnan;

// Time
unsigned long millis();

// Math helpers
#ifndef min
#define min(a,b) ((a)<(b)?(a):(b))
#endif
#ifndef max
#define max(a,b) ((a)>(b)?(a):(b))
#endif
#ifndef abs
#define abs(x) ((x)>0?(x):-(x))
#endif

// Print base
#define DEC 10
#define HEX 16
#define OCT 8
#define BIN 2

// Forward declarations
class Print {
public:
    virtual size_t write(uint8_t c) = 0;
    virtual size_t write(const uint8_t *buf, size_t size);

    size_t print(const char *str);
    size_t print(char c);
    size_t print(int val, int base = DEC);
    size_t print(unsigned int val, int base = DEC);
    size_t print(long val, int base = DEC);
    size_t print(unsigned long val, int base = DEC);
    size_t print(double val, int digits = 2);
    size_t println();
    size_t println(const char *str);

    virtual ~Print() {}
};

class Stream : public Print {
public:
    virtual int available() = 0;
    virtual int read() = 0;
    virtual int peek() = 0;
    virtual size_t readBytes(char *buffer, size_t length);

    virtual ~Stream() {}
};

// String copy with size limit (Arduino-compatible)
#ifndef strlcpy
size_t strlcpy(char *dst, const char *src, size_t size);
#endif

#endif // ARDUINO_H_SHIM
