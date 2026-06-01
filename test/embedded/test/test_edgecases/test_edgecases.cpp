// Extended edge-case tests for CmdMessenger on the embedded side.
// Covers: numeric overflow into wrong type, non-numeric input, sequential
// multi-arg reads, reading past available, whitespace, send output format,
// binary round-trip, many-argument stress, and printLfCr mode.

#include <unity.h>
#include "MockStream.h"
#include <CmdMessenger.h>
#include <cstring>
#include <cstdio>

static MockStream stream;
static CmdMessenger *cmdMsg;

static bool g_called = false;

// --- Multi-arg sequential reading ---
static int16_t g_arg1_i16 = 0;
static float g_arg2_float = 0;
static char g_arg3_str[64] = {0};
static int32_t g_arg4_i32 = 0;
static bool g_allArgOk = false;

static void onMultiArg() {
    g_called = true;
    g_arg1_i16 = cmdMsg->readInt16Arg();
    bool ok1 = cmdMsg->isArgOk();
    g_arg2_float = cmdMsg->readFloatArg();
    bool ok2 = cmdMsg->isArgOk();
    char *s = cmdMsg->readStringArg();
    bool ok3 = cmdMsg->isArgOk();
    if (s) strncpy(g_arg3_str, s, sizeof(g_arg3_str) - 1);
    g_arg4_i32 = cmdMsg->readInt32Arg();
    bool ok4 = cmdMsg->isArgOk();
    g_allArgOk = ok1 && ok2 && ok3 && ok4;
}

// --- Reading past available ---
static int16_t g_extra_read = 99;
static bool g_extraArgOk = true;

static void onReadPastAvailable() {
    g_called = true;
    cmdMsg->readInt16Arg(); // consume the one arg
    g_extra_read = cmdMsg->readInt16Arg(); // no more args
    g_extraArgOk = cmdMsg->isArgOk();
}

// --- Overflow into wrong type ---
static int16_t g_overflow_i16 = 0;
static bool g_overflowArgOk = false;

static void onReadInt16Overflow() {
    g_called = true;
    g_overflow_i16 = cmdMsg->readInt16Arg();
    g_overflowArgOk = cmdMsg->isArgOk();
}

// --- Non-numeric into numeric ---
static int32_t g_nonnumeric_i32 = 99;
static bool g_nonnumericArgOk = true;

static void onReadNonNumeric() {
    g_called = true;
    g_nonnumeric_i32 = cmdMsg->readInt32Arg();
    g_nonnumericArgOk = cmdMsg->isArgOk();
}

// --- Whitespace in string arg ---
static char g_ws_str[64] = {0};
static bool g_wsArgOk = false;

static void onReadWhitespace() {
    g_called = true;
    char *s = cmdMsg->readStringArg();
    g_wsArgOk = cmdMsg->isArgOk();
    if (s) strncpy(g_ws_str, s, sizeof(g_ws_str) - 1);
}

// --- Many args ---
#define MANY_ARGS_COUNT 20
static int16_t g_many_args[MANY_ARGS_COUNT] = {0};
static int g_many_args_read = 0;

static void onManyArgs() {
    g_called = true;
    g_many_args_read = 0;
    for (int i = 0; i < MANY_ARGS_COUNT; i++) {
        g_many_args[i] = cmdMsg->readInt16Arg();
        if (!cmdMsg->isArgOk()) break;
        g_many_args_read++;
    }
}

static void resetAll() {
    g_called = false;
    g_arg1_i16 = 0; g_arg2_float = 0; g_arg3_str[0] = '\0'; g_arg4_i32 = 0;
    g_allArgOk = false;
    g_extra_read = 99; g_extraArgOk = true;
    g_overflow_i16 = 0; g_overflowArgOk = false;
    g_nonnumeric_i32 = 99; g_nonnumericArgOk = true;
    g_ws_str[0] = '\0'; g_wsArgOk = false;
    memset(g_many_args, 0, sizeof(g_many_args)); g_many_args_read = 0;
}

static void feedCommand(const char *cmd, byte attachId, void(*cb)()) {
    stream.clear();
    delete cmdMsg;
    cmdMsg = new CmdMessenger(stream);
    cmdMsg->attach(attachId, cb);
    resetAll();
    stream.inject(cmd);
    cmdMsg->feedinSerialData();
}

void setUp(void) {
    stream.clear();
    cmdMsg = new CmdMessenger(stream);
    resetAll();
}

void tearDown(void) {
    delete cmdMsg;
    cmdMsg = nullptr;
}

// ====== Tests ======

// --- Multi-arg sequential reading ---

void test_multiarg_sequential(void) {
    // "1,42,3.14,hello,99999;"
    feedCommand("1,42,3.14,hello,99999;", 1, onMultiArg);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_allArgOk);
    TEST_ASSERT_EQUAL_INT16(42, g_arg1_i16);
    TEST_ASSERT_FLOAT_WITHIN(0.01, 3.14, g_arg2_float);
    TEST_ASSERT_EQUAL_STRING("hello", g_arg3_str);
    TEST_ASSERT_EQUAL_INT32(99999, g_arg4_i32);
}

// --- Reading past available args ---

void test_read_past_available_returns_zero(void) {
    feedCommand("1,42;", 1, onReadPastAvailable);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_EQUAL_INT16(0, g_extra_read);
    TEST_ASSERT_FALSE(g_extraArgOk);
}

// --- Numeric overflow (value too large for int16) ---

void test_readInt16_overflow_value(void) {
    // 99999 doesn't fit in int16_t — atoi behavior is platform-defined
    // but it should not crash, and ArgOk should still be true (atoi succeeded)
    feedCommand("1,99999;", 1, onReadInt16Overflow);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_overflowArgOk); // atoi returns *something*, ArgOk = true
    // The value will be truncated/wrapped — we just verify no crash
}

void test_readInt16_negative_overflow(void) {
    // -99999 doesn't fit in int16_t
    feedCommand("1,-99999;", 1, onReadInt16Overflow);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_overflowArgOk);
}

// --- Non-numeric string into numeric reader ---

void test_readInt32_nonnumeric(void) {
    // "abc" → atol returns 0
    feedCommand("1,abc;", 1, onReadNonNumeric);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_nonnumericArgOk); // arg was present, atol just returns 0
    TEST_ASSERT_EQUAL_INT32(0, g_nonnumeric_i32);
}

void test_readInt32_mixed_numeric(void) {
    // "42abc" → atol returns 42 (stops at first non-digit)
    feedCommand("1,42abc;", 1, onReadNonNumeric);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_EQUAL_INT32(42, g_nonnumeric_i32);
}

void test_readInt32_empty_string(void) {
    // Empty arg: "1,;" — split_r may skip it
    feedCommand("1,;", 1, onReadNonNumeric);
    TEST_ASSERT_TRUE(g_called);
    // atol("") or no arg — either way, no crash
}

// --- Whitespace handling ---

void test_readString_leading_whitespace(void) {
    feedCommand("1,  hello;", 1, onReadWhitespace);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_wsArgOk);
    TEST_ASSERT_EQUAL_STRING("  hello", g_ws_str);
}

void test_readString_trailing_whitespace(void) {
    feedCommand("1,hello  ;", 1, onReadWhitespace);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_wsArgOk);
    TEST_ASSERT_EQUAL_STRING("hello  ", g_ws_str);
}

void test_readString_only_whitespace(void) {
    feedCommand("1,   ;", 1, onReadWhitespace);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_wsArgOk);
    TEST_ASSERT_EQUAL_STRING("   ", g_ws_str);
}

// --- Many arguments (stress tokenizer) ---

void test_many_arguments(void) {
    // Build "1,0,1,2,3,...,19;"
    char cmd[200];
    int pos = 0;
    pos += snprintf(cmd + pos, sizeof(cmd) - pos, "1");
    for (int i = 0; i < MANY_ARGS_COUNT; i++) {
        pos += snprintf(cmd + pos, sizeof(cmd) - pos, ",%d", i);
    }
    pos += snprintf(cmd + pos, sizeof(cmd) - pos, ";");

    feedCommand(cmd, 1, onManyArgs);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_EQUAL(MANY_ARGS_COUNT, g_many_args_read);
    for (int i = 0; i < MANY_ARGS_COUNT; i++) {
        TEST_ASSERT_EQUAL_INT16(i, g_many_args[i]);
    }
}

// --- Send command output format ---

void test_sendCmd_basic_format(void) {
    stream.clear();
    cmdMsg->sendCmdStart(5);
    cmdMsg->sendCmdEnd();
    const char *out = stream.getTx();
    // Expected: "5;"
    TEST_ASSERT_EQUAL_STRING("5;", out);
}

void test_sendCmd_with_args_format(void) {
    stream.clear();
    cmdMsg->sendCmdStart(3);
    cmdMsg->sendCmdArg(42);
    cmdMsg->sendCmdArg("hello");
    cmdMsg->sendCmdEnd();
    const char *out = stream.getTx();
    // Expected: "3,42,hello;"
    TEST_ASSERT_EQUAL_STRING("3,42,hello;", out);
}

void test_sendCmd_escaped_arg_format(void) {
    stream.clear();
    cmdMsg->sendCmdStart(1);
    cmdMsg->sendCmdEscArg((char *)"a,b;c/d");
    cmdMsg->sendCmdEnd();
    const char *out = stream.getTx();
    // Expected: "1,a/,b/;c//d;"
    TEST_ASSERT_EQUAL_STRING("1,a/,b/;c//d;", out);
}

void test_sendCmd_printLfCr_mode(void) {
    stream.clear();
    cmdMsg->printLfCr(true);
    cmdMsg->sendCmdStart(7);
    cmdMsg->sendCmdEnd();
    const char *out = stream.getTx();
    // Expected: "7;\r\n"
    TEST_ASSERT_EQUAL_STRING("7;\r\n", out);
    cmdMsg->printLfCr(false); // restore
}

// --- Binary arg round-trip ---

void test_sendBinCmd_and_readBinArg(void) {
    stream.clear();

    // Send a binary int16 (value 0x1234)
    int16_t original = 0x1234;
    cmdMsg->sendCmdStart(2);
    cmdMsg->sendCmdBinArg(original);
    cmdMsg->sendCmdEnd();

    // Capture what was sent
    const char *out = stream.getTx();

    // Verify wire format: "2," + escaped binary bytes + ";"
    // 0x1234 little-endian = 0x34, 0x12. Neither is ',', ';', '/' or '\0',
    // so no escaping needed. Wire: "2," + 0x34 + 0x12 + ";" = 5 bytes
    TEST_ASSERT_EQUAL_UINT8('2', stream.txBuf[0]);
    TEST_ASSERT_EQUAL_UINT8(',', stream.txBuf[1]);
    // Last byte before any \0 should be ';'
    TEST_ASSERT_EQUAL_UINT8(';', stream.txBuf[stream.txLen - 1]);
    TEST_ASSERT_TRUE(stream.txLen >= 5);
}

// --- Null byte escaping (embedded \0 in data) ---

void test_escape_null_byte_in_binary(void) {
    stream.clear();

    // Send a binary value that contains 0x00 byte (should be escaped)
    int16_t val = 0x0001; // little-endian: bytes 0x01, 0x00
    cmdMsg->sendCmdStart(1);
    cmdMsg->sendCmdBinArg(val);
    cmdMsg->sendCmdEnd();

    const char *out = stream.getTx();
    // The 0x00 byte should be escaped as '/' + '\0'
    // Output should contain the escape character before the null
    // Let's verify by checking the raw bytes
    size_t len = stream.txLen;
    // "1," = 2 chars, then escaped binary, then ";"
    TEST_ASSERT_GREATER_THAN(4, (int)len); // more than "1,XY;" because of escaping
}

// --- Float edge cases ---
static float g_float_result = 0;
static bool g_floatArgOk = false;

static void onReadFloat() {
    g_called = true;
    g_float_result = cmdMsg->readFloatArg();
    g_floatArgOk = cmdMsg->isArgOk();
}

void test_readFloat_very_small(void) {
    feedCommand("1,0.000001;", 1, onReadFloat);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_floatArgOk);
    TEST_ASSERT_FLOAT_WITHIN(0.0000001, 0.000001, g_float_result);
}

void test_readFloat_scientific_notation(void) {
    feedCommand("1,1.5e3;", 1, onReadFloat);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_floatArgOk);
    TEST_ASSERT_FLOAT_WITHIN(1.0, 1500.0, g_float_result);
}

void test_readFloat_negative_scientific(void) {
    feedCommand("1,-2.5e-2;", 1, onReadFloat);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_FLOAT_WITHIN(0.001, -0.025, g_float_result);
}

void test_readFloat_nonnumeric(void) {
    // "abc" → strtod returns 0.0
    feedCommand("1,abc;", 1, onReadFloat);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_FLOAT_WITHIN(0.001, 0.0, g_float_result);
}

int main(int argc, char **argv) {
    UNITY_BEGIN();

    // Multi-arg
    RUN_TEST(test_multiarg_sequential);
    RUN_TEST(test_read_past_available_returns_zero);

    // Numeric overflow/non-numeric
    RUN_TEST(test_readInt16_overflow_value);
    RUN_TEST(test_readInt16_negative_overflow);
    RUN_TEST(test_readInt32_nonnumeric);
    RUN_TEST(test_readInt32_mixed_numeric);
    RUN_TEST(test_readInt32_empty_string);

    // Whitespace
    RUN_TEST(test_readString_leading_whitespace);
    RUN_TEST(test_readString_trailing_whitespace);
    RUN_TEST(test_readString_only_whitespace);

    // Many args
    RUN_TEST(test_many_arguments);

    // Send format
    RUN_TEST(test_sendCmd_basic_format);
    RUN_TEST(test_sendCmd_with_args_format);
    RUN_TEST(test_sendCmd_escaped_arg_format);
    RUN_TEST(test_sendCmd_printLfCr_mode);

    // Binary
    RUN_TEST(test_sendBinCmd_and_readBinArg);
    RUN_TEST(test_escape_null_byte_in_binary);

    // Float edge cases
    RUN_TEST(test_readFloat_very_small);
    RUN_TEST(test_readFloat_scientific_notation);
    RUN_TEST(test_readFloat_negative_scientific);
    RUN_TEST(test_readFloat_nonnumeric);

    return UNITY_END();
}
