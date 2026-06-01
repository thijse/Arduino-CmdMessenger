// Tests for callback attach/detach/dispatch behavior.

#include <unity.h>
#include "MockStream.h"
#include <CmdMessenger.h>

static MockStream stream;
static CmdMessenger *cmdMsg;

static int g_defaultCount = 0;
static int g_cmd0Count = 0;
static int g_cmd1Count = 0;
static int g_cmd2Count = 0;
static int g_cmd49Count = 0;

static void resetCounts() {
    g_defaultCount = 0;
    g_cmd0Count = 0;
    g_cmd1Count = 0;
    g_cmd2Count = 0;
    g_cmd49Count = 0;
}

static void onDefault() { g_defaultCount++; }
static void onCmd0() { g_cmd0Count++; }
static void onCmd1() { g_cmd1Count++; }
static void onCmd2() { g_cmd2Count++; }
static void onCmd49() { g_cmd49Count++; }

void setUp(void) {
    stream.clear();
    if (cmdMsg) delete cmdMsg;
    cmdMsg = new CmdMessenger(stream);
    resetCounts();
}

void tearDown(void) {
    delete cmdMsg;
    cmdMsg = nullptr;
}

// --- Tests ---

void test_default_callback_fires_for_unknown(void) {
    cmdMsg->attach(onDefault);
    stream.inject("99;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, g_defaultCount);
}

void test_specific_callback_fires(void) {
    cmdMsg->attach(1, onCmd1);
    stream.inject("1;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, g_cmd1Count);
    TEST_ASSERT_EQUAL(0, g_defaultCount);
}

void test_specific_overrides_default(void) {
    cmdMsg->attach(onDefault);
    cmdMsg->attach(1, onCmd1);
    stream.inject("1;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, g_cmd1Count);
    TEST_ASSERT_EQUAL(0, g_defaultCount);
}

void test_unattached_id_falls_to_default(void) {
    cmdMsg->attach(onDefault);
    cmdMsg->attach(1, onCmd1);
    stream.inject("5;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(0, g_cmd1Count);
    TEST_ASSERT_EQUAL(1, g_defaultCount);
}

void test_multiple_different_callbacks(void) {
    cmdMsg->attach(0, onCmd0);
    cmdMsg->attach(1, onCmd1);
    cmdMsg->attach(2, onCmd2);
    stream.inject("0;1;2;1;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, g_cmd0Count);
    TEST_ASSERT_EQUAL(2, g_cmd1Count);
    TEST_ASSERT_EQUAL(1, g_cmd2Count);
}

void test_max_callback_id(void) {
    // MAXCALLBACKS = 50, so ID 49 is the highest valid
    cmdMsg->attach(49, onCmd49);
    stream.inject("49;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, g_cmd49Count);
}

void test_overwrite_callback(void) {
    cmdMsg->attach(1, onCmd1);
    // Overwrite with cmd2 handler
    cmdMsg->attach(1, onCmd2);
    stream.inject("1;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(0, g_cmd1Count);
    TEST_ASSERT_EQUAL(1, g_cmd2Count);
}

void test_no_callbacks_attached(void) {
    // No attach at all — should not crash
    stream.inject("1;2;3;");
    cmdMsg->feedinSerialData();
    // No assertions needed beyond "no crash"
    TEST_ASSERT_TRUE(true);
}

void test_command_id_beyond_max_uses_default(void) {
    cmdMsg->attach(onDefault);
    // ID 50 is >= MAXCALLBACKS, should fall to default
    stream.inject("50;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, g_defaultCount);
}

void test_command_id_0(void) {
    cmdMsg->attach(0, onCmd0);
    stream.inject("0;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(1, g_cmd0Count);
}

void test_commandID_returns_last(void) {
    cmdMsg->attach(2, onCmd2);
    stream.inject("2;");
    cmdMsg->feedinSerialData();
    TEST_ASSERT_EQUAL(2, cmdMsg->commandID());
}

int main(int argc, char **argv) {
    UNITY_BEGIN();
    RUN_TEST(test_default_callback_fires_for_unknown);
    RUN_TEST(test_specific_callback_fires);
    RUN_TEST(test_specific_overrides_default);
    RUN_TEST(test_unattached_id_falls_to_default);
    RUN_TEST(test_multiple_different_callbacks);
    RUN_TEST(test_max_callback_id);
    RUN_TEST(test_overwrite_callback);
    RUN_TEST(test_no_callbacks_attached);
    RUN_TEST(test_command_id_beyond_max_uses_default);
    RUN_TEST(test_command_id_0);
    RUN_TEST(test_commandID_returns_last);
    return UNITY_END();
}
