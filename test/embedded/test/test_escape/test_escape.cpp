// Tests for escape/unescape symmetry on the embedded side.
// Verifies printEsc() output can be correctly unescaped by unescape().

#include <unity.h>
#include "MockStream.h"
#include <CmdMessenger.h>

static MockStream stream;
static CmdMessenger *cmdMsg;

void setUp(void) {
    stream.clear();
    if (cmdMsg) delete cmdMsg;
    cmdMsg = new CmdMessenger(stream);
}

void tearDown(void) {
    delete cmdMsg;
    cmdMsg = nullptr;
}

// Helper: send a command with escaped string arg, capture output,
// then feed it back and read the string arg — should match original.
static void roundTrip(const char *input, const char *expected) {
    stream.clearTx();
    cmdMsg->sendCmdStart(1);
    cmdMsg->sendCmdEscArg((char *)input);
    cmdMsg->sendCmdEnd();

    // Output should be "1,<escaped>;\r\n" or "1,<escaped>;"
    const char *output = stream.getTx();

    // Now feed this back as incoming and read the arg
    stream.clearRx();
    stream.inject(output);
    
    // Set up a fresh messenger to receive
    MockStream stream2;
    stream2.inject(output);
    CmdMessenger receiver(stream2);
    
    static char receivedArg[256] = {0};
    static bool called = false;
    called = false;
    
    receiver.attach(1, [](){ called = true; });
    receiver.feedinSerialData();

    // We can't easily use lambdas with the C callback, so verify manually:
    // Parse the received output by creating another messenger
    // Instead, let's just verify the escaped output format directly.
    
    // Alternative: verify unescape works on the escaped portion
    // Extract the argument portion between first ',' and ';'
    const char *comma = strchr(output, ',');
    const char *semi = strrchr(output, ';');
    
    TEST_ASSERT_NOT_NULL(comma);
    TEST_ASSERT_NOT_NULL(semi);
    
    size_t argLen = semi - comma - 1;
    char argBuf[256];
    TEST_ASSERT_LESS_THAN(sizeof(argBuf), argLen);
    memcpy(argBuf, comma + 1, argLen);
    argBuf[argLen] = '\0';
    
    // Unescape in-place
    cmdMsg->unescape(argBuf);
    
    TEST_ASSERT_EQUAL_STRING(expected, argBuf);
}

// --- Tests ---

void test_escape_plain_text(void) {
    roundTrip("hello", "hello");
}

void test_escape_field_separator(void) {
    roundTrip("a,b", "a,b");
}

void test_escape_command_separator(void) {
    roundTrip("a;b", "a;b");
}

void test_escape_escape_char(void) {
    roundTrip("a/b", "a/b");
}

void test_escape_all_special(void) {
    roundTrip(",;/", ",;/");
}

void test_escape_empty_string(void) {
    roundTrip("", "");
}

void test_escape_only_separators(void) {
    roundTrip(",,;;", ",,;;");
}

void test_escape_long_string(void) {
    // 200 chars with embedded specials
    char input[201];
    for (int i = 0; i < 200; i++) {
        input[i] = (i % 10 == 0) ? ',' : ('A' + (i % 26));
    }
    input[200] = '\0';
    roundTrip(input, input);
}

void test_unescape_no_escape_chars(void) {
    char buf[] = "hello world";
    cmdMsg->unescape(buf);
    TEST_ASSERT_EQUAL_STRING("hello world", buf);
}

void test_unescape_escaped_comma(void) {
    char buf[] = "a/,b";
    cmdMsg->unescape(buf);
    TEST_ASSERT_EQUAL_STRING("a,b", buf);
}

void test_unescape_escaped_semicolon(void) {
    char buf[] = "a/;b";
    cmdMsg->unescape(buf);
    TEST_ASSERT_EQUAL_STRING("a;b", buf);
}

void test_unescape_escaped_slash(void) {
    char buf[] = "a//b";
    cmdMsg->unescape(buf);
    TEST_ASSERT_EQUAL_STRING("a/b", buf);
}

void test_unescape_trailing_escape(void) {
    // Trailing escape with no following char — should not crash
    char buf[] = "hello/";
    cmdMsg->unescape(buf);
    // After unescape: the '/' is consumed as escape, then *fromChar++ reads '\0'
    // which gets written, so result may be "hello\0" or just "hello"
    // The key assertion: it must not crash and strlen <= original
    TEST_ASSERT_LESS_OR_EQUAL(6, strlen(buf));
}

void test_unescape_empty(void) {
    char buf[] = "";
    cmdMsg->unescape(buf);
    TEST_ASSERT_EQUAL_STRING("", buf);
}

int main(int argc, char **argv) {
    UNITY_BEGIN();
    RUN_TEST(test_escape_plain_text);
    RUN_TEST(test_escape_field_separator);
    RUN_TEST(test_escape_command_separator);
    RUN_TEST(test_escape_escape_char);
    RUN_TEST(test_escape_all_special);
    RUN_TEST(test_escape_empty_string);
    RUN_TEST(test_escape_only_separators);
    RUN_TEST(test_escape_long_string);
    RUN_TEST(test_unescape_no_escape_chars);
    RUN_TEST(test_unescape_escaped_comma);
    RUN_TEST(test_unescape_escaped_semicolon);
    RUN_TEST(test_unescape_escaped_slash);
    RUN_TEST(test_unescape_trailing_escape);
    RUN_TEST(test_unescape_empty);
    return UNITY_END();
}
