// Tests for feedinSerialData(): byte-at-a-time, chunked, all-at-once delivery.

#include <unity.h>
#include "MockStream.h"
#include <CmdMessenger.h>

static MockStream stream;
static CmdMessenger *cmdMsg;

// Callback tracking
static int lastCallbackId = -1;
static int callbackCount = 0;

static void resetTracking() {
    lastCallbackId = -1;
    callbackCount = 0;
}

static void onCmd0() { lastCallbackId = 0; callbackCount++; }
static void onCmd1() { lastCallbackId = 1; callbackCount++; }
static void onCmd2() { lastCallbackId = 2; callbackCount++; }
static void onDefault() { lastCallbackId = 99; callbackCount++; }

void setUp(void) {
    stream.clear();
    if (cmdMsg) delete cmdMsg;
    cmdMsg = new CmdMessenger(stream);
    cmdMsg->attach(onDefault);
    cmdMsg->attach(0, onCmd0);
    cmdMsg->attach(1, onCmd1);
    cmdMsg->attach(2, onCmd2);
    resetTracking();
}

void tearDown(void) {
    delete cmdMsg;
    cmdMsg = nullptr;
}

// --- Tests ---

void test_single_command_all_at_once(void) {
    stream.inject("1;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, callbackCount);
    TEST_ASSERT_EQUAL(1, lastCallbackId);
}

void test_single_command_byte_at_a_time(void) {
    const char *msg = "2;";
    for (size_t i = 0; i < strlen(msg); i++) {
        stream.inject((const uint8_t *)&msg[i], 1);
        cmdMsg->feedinSerialData();
    }
    TEST_ASSERT_EQUAL(1, callbackCount);
    TEST_ASSERT_EQUAL(2, lastCallbackId);
}

void test_single_command_chunked(void) {
    // Send "1,hello;" in two chunks
    stream.inject("1,hel");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(0, callbackCount); // not complete yet

    stream.inject("lo;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, callbackCount);
    TEST_ASSERT_EQUAL(1, lastCallbackId);
}

void test_multiple_commands_in_one_buffer(void) {
    stream.inject("0;1;2;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(3, callbackCount);
    TEST_ASSERT_EQUAL(2, lastCallbackId); // last one dispatched
}

void test_empty_between_separators_ignored(void) {
    // ";;" should not dispatch anything (empty between separators = bufferIndex == 0)
    stream.inject(";;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(0, callbackCount);
}

void test_command_with_arguments(void) {
    stream.inject("1,42,hello;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, callbackCount);
    TEST_ASSERT_EQUAL(1, lastCallbackId);
}

void test_no_data_available(void) {
    // No inject — should not crash or dispatch
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(0, callbackCount);
}

void test_partial_never_completes(void) {
    // Inject incomplete command (no separator)
    stream.inject("1,hello");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(0, callbackCount);
}

int main(int argc, char **argv) {
    UNITY_BEGIN();
    RUN_TEST(test_single_command_all_at_once);
    RUN_TEST(test_single_command_byte_at_a_time);
    RUN_TEST(test_single_command_chunked);
    RUN_TEST(test_multiple_commands_in_one_buffer);
    RUN_TEST(test_empty_between_separators_ignored);
    RUN_TEST(test_command_with_arguments);
    RUN_TEST(test_no_data_available);
    RUN_TEST(test_partial_never_completes);
    return UNITY_END();
}
