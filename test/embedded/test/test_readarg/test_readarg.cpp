// Tests for readXxxArg() boundary conditions on the embedded side.

#include <unity.h>
#include "MockStream.h"
#include <CmdMessenger.h>

static MockStream stream;
static CmdMessenger *cmdMsg;

// We use a pattern: inject a command, call feedinSerialData, then in the
// callback read the arguments. Since C function pointers can't capture,
// we use globals.

static int16_t g_int16 = 0;
static int32_t g_int32 = 0;
static float g_float = 0.0f;
static double g_double = 0.0;
static char g_string[128] = {0};
static bool g_argOk = false;
static bool g_called = false;
static char g_char = 0;
static bool g_bool = false;

static void resetGlobals() {
    g_int16 = 0; g_int32 = 0; g_float = 0; g_double = 0;
    g_string[0] = '\0'; g_argOk = false; g_called = false;
    g_char = 0; g_bool = false;
}

// Callback that reads an int16 arg
static void onReadInt16() {
    g_called = true;
    g_int16 = cmdMsg->readInt16Arg();
    g_argOk = cmdMsg->isArgOk();
}

static void onReadInt32() {
    g_called = true;
    g_int32 = cmdMsg->readInt32Arg();
    g_argOk = cmdMsg->isArgOk();
}

static void onReadFloat() {
    g_called = true;
    g_float = cmdMsg->readFloatArg();
    g_argOk = cmdMsg->isArgOk();
}

static void onReadDouble() {
    g_called = true;
    g_double = cmdMsg->readDoubleArg();
    g_argOk = cmdMsg->isArgOk();
}

static void onReadString() {
    g_called = true;
    char *s = cmdMsg->readStringArg();
    g_argOk = cmdMsg->isArgOk();
    if (s) {
        strncpy(g_string, s, sizeof(g_string) - 1);
        g_string[sizeof(g_string) - 1] = '\0';
    } else {
        g_string[0] = '\0';
    }
}

static void onReadChar() {
    g_called = true;
    g_char = cmdMsg->readCharArg();
    g_argOk = cmdMsg->isArgOk();
}

static void onReadBool() {
    g_called = true;
    g_bool = cmdMsg->readBoolArg();
    g_argOk = cmdMsg->isArgOk();
}

// Helper to set up, inject and process
static void feedCommand(const char *cmd, byte attachId, void(*cb)()) {
    stream.clear();
    delete cmdMsg;
    cmdMsg = new CmdMessenger(stream);
    cmdMsg->attach(attachId, cb);
    resetGlobals();
    stream.inject(cmd);
    cmdMsg->feedinSerialData();
}

void setUp(void) {
    stream.clear();
    cmdMsg = new CmdMessenger(stream);
    resetGlobals();
}

void tearDown(void) {
    delete cmdMsg;
    cmdMsg = nullptr;
}

// --- Int16 tests ---

void test_readInt16_normal(void) {
    feedCommand("1,42;", 1, onReadInt16);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_TRUE(g_argOk);
    TEST_ASSERT_EQUAL_INT16(42, g_int16);
}

void test_readInt16_negative(void) {
    feedCommand("1,-100;", 1, onReadInt16);
    TEST_ASSERT_EQUAL_INT16(-100, g_int16);
}

void test_readInt16_zero(void) {
    feedCommand("1,0;", 1, onReadInt16);
    TEST_ASSERT_EQUAL_INT16(0, g_int16);
    TEST_ASSERT_TRUE(g_argOk);
}

void test_readInt16_max(void) {
    feedCommand("1,32767;", 1, onReadInt16);
    TEST_ASSERT_EQUAL_INT16(32767, g_int16);
}

void test_readInt16_min(void) {
    feedCommand("1,-32768;", 1, onReadInt16);
    TEST_ASSERT_EQUAL_INT16(-32768, g_int16);
}

void test_readInt16_no_arg(void) {
    // Command with no argument after ID
    feedCommand("1;", 1, onReadInt16);
    TEST_ASSERT_TRUE(g_called);
    TEST_ASSERT_FALSE(g_argOk);
    TEST_ASSERT_EQUAL_INT16(0, g_int16);
}

// --- Int32 tests ---

void test_readInt32_normal(void) {
    feedCommand("1,123456;", 1, onReadInt32);
    TEST_ASSERT_EQUAL_INT32(123456, g_int32);
    TEST_ASSERT_TRUE(g_argOk);
}

void test_readInt32_max(void) {
    feedCommand("1,2147483647;", 1, onReadInt32);
    TEST_ASSERT_EQUAL_INT32(2147483647L, g_int32);
}

void test_readInt32_min(void) {
    // atol("-2147483648") may have platform-specific behavior
    feedCommand("1,-2147483648;", 1, onReadInt32);
    TEST_ASSERT_EQUAL_INT32(-2147483648L, g_int32);
}

void test_readInt32_no_arg(void) {
    feedCommand("1;", 1, onReadInt32);
    TEST_ASSERT_FALSE(g_argOk);
    TEST_ASSERT_EQUAL_INT32(0, g_int32);
}

// --- Float tests ---

void test_readFloat_normal(void) {
    feedCommand("1,3.14;", 1, onReadFloat);
    TEST_ASSERT_FLOAT_WITHIN(0.01, 3.14, g_float);
    TEST_ASSERT_TRUE(g_argOk);
}

void test_readFloat_negative(void) {
    feedCommand("1,-1.5;", 1, onReadFloat);
    TEST_ASSERT_FLOAT_WITHIN(0.01, -1.5, g_float);
}

void test_readFloat_zero(void) {
    feedCommand("1,0.0;", 1, onReadFloat);
    TEST_ASSERT_FLOAT_WITHIN(0.001, 0.0, g_float);
    TEST_ASSERT_TRUE(g_argOk);
}

void test_readFloat_no_arg(void) {
    feedCommand("1;", 1, onReadFloat);
    TEST_ASSERT_FALSE(g_argOk);
    TEST_ASSERT_FLOAT_WITHIN(0.001, 0.0, g_float);
}

void test_readFloat_large(void) {
    feedCommand("1,1000000.5;", 1, onReadFloat);
    TEST_ASSERT_FLOAT_WITHIN(1.0, 1000000.5, g_float);
}

// --- Double tests ---

void test_readDouble_normal(void) {
    feedCommand("1,3.14159265;", 1, onReadDouble);
    // Use float-level precision since Unity may not have double enabled
    TEST_ASSERT_FLOAT_WITHIN(0.001, 3.14159265, (float)g_double);
    TEST_ASSERT_TRUE(g_argOk);
}

// --- String tests ---

void test_readString_normal(void) {
    feedCommand("1,hello;", 1, onReadString);
    TEST_ASSERT_TRUE(g_argOk);
    TEST_ASSERT_EQUAL_STRING("hello", g_string);
}

void test_readString_with_escaped_comma(void) {
    feedCommand("1,a/,b;", 1, onReadString);
    TEST_ASSERT_EQUAL_STRING("a,b", g_string);
}

void test_readString_with_escaped_semicolon(void) {
    feedCommand("1,a/;b;", 1, onReadString);
    TEST_ASSERT_EQUAL_STRING("a;b", g_string);
}

void test_readString_with_escaped_slash(void) {
    feedCommand("1,a//b;", 1, onReadString);
    TEST_ASSERT_EQUAL_STRING("a/b", g_string);
}

void test_readString_empty(void) {
    // "1,;" — empty string argument
    feedCommand("1,;", 1, onReadString);
    // split_r strips leading delimiters, so this may or may not yield an arg
    // The important thing: no crash
    TEST_ASSERT_TRUE(g_called);
}

void test_readString_no_arg(void) {
    feedCommand("1;", 1, onReadString);
    TEST_ASSERT_FALSE(g_argOk);
}

// --- Char tests ---

void test_readChar_normal(void) {
    feedCommand("1,X;", 1, onReadChar);
    TEST_ASSERT_EQUAL_CHAR('X', g_char);
    TEST_ASSERT_TRUE(g_argOk);
}

void test_readChar_no_arg(void) {
    feedCommand("1;", 1, onReadChar);
    TEST_ASSERT_FALSE(g_argOk);
    TEST_ASSERT_EQUAL_CHAR(0, g_char);
}

// --- Bool tests ---

void test_readBool_true(void) {
    feedCommand("1,1;", 1, onReadBool);
    TEST_ASSERT_TRUE(g_bool);
}

void test_readBool_false(void) {
    feedCommand("1,0;", 1, onReadBool);
    TEST_ASSERT_FALSE(g_bool);
}

void test_readBool_nonzero_is_true(void) {
    feedCommand("1,5;", 1, onReadBool);
    TEST_ASSERT_TRUE(g_bool);
}

int main(int argc, char **argv) {
    UNITY_BEGIN();
    // Int16
    RUN_TEST(test_readInt16_normal);
    RUN_TEST(test_readInt16_negative);
    RUN_TEST(test_readInt16_zero);
    RUN_TEST(test_readInt16_max);
    RUN_TEST(test_readInt16_min);
    RUN_TEST(test_readInt16_no_arg);
    // Int32
    RUN_TEST(test_readInt32_normal);
    RUN_TEST(test_readInt32_max);
    RUN_TEST(test_readInt32_min);
    RUN_TEST(test_readInt32_no_arg);
    // Float
    RUN_TEST(test_readFloat_normal);
    RUN_TEST(test_readFloat_negative);
    RUN_TEST(test_readFloat_zero);
    RUN_TEST(test_readFloat_no_arg);
    RUN_TEST(test_readFloat_large);
    // Double
    RUN_TEST(test_readDouble_normal);
    // String
    RUN_TEST(test_readString_normal);
    RUN_TEST(test_readString_with_escaped_comma);
    RUN_TEST(test_readString_with_escaped_semicolon);
    RUN_TEST(test_readString_with_escaped_slash);
    RUN_TEST(test_readString_empty);
    RUN_TEST(test_readString_no_arg);
    // Char
    RUN_TEST(test_readChar_normal);
    RUN_TEST(test_readChar_no_arg);
    // Bool
    RUN_TEST(test_readBool_true);
    RUN_TEST(test_readBool_false);
    RUN_TEST(test_readBool_nonzero_is_true);
    return UNITY_END();
}
