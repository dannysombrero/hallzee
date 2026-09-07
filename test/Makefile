CLANG := xcrun clang++
LLVM_PROFDATA := xcrun llvm-profdata
LLVM_COV := xcrun llvm-cov
BUILD_DIR := build
TEST_BINARY := $(BUILD_DIR)/native-tests
PROFILE_RAW := $(BUILD_DIR)/default.profraw
PROFILE_DATA := $(BUILD_DIR)/coverage.profdata

CXXFLAGS := -std=c++17 -Wall -Wextra -Werror -I../test/support -I.. \
	-fprofile-instr-generate -fcoverage-mapping

.PHONY: test coverage clean

test: $(TEST_BINARY)
	LLVM_PROFILE_FILE=$(PROFILE_RAW) $(TEST_BINARY)

$(TEST_BINARY): test_terminal_display.cpp ../TerminalDisplay.cpp ../TerminalDisplay.h ../TerminalController.cpp ../TerminalController.h ../BellPolicy.cpp ../BellPolicy.h ../KeypadController.cpp ../KeypadController.h ../BluetoothSync.cpp ../BluetoothSync.h ../BluetoothSerialPort.h ../TripRecordCodec.cpp ../TripRecordCodec.h ../KeypadPort.h ../MonotonicClock.h ../TripStoragePort.h ../StudentIdPolicy.h ../TimeProvider.h ../DisplayPort.h ../DisplayColors.h ../Config.h ../AppTypes.h
	mkdir -p $(BUILD_DIR)
	$(CLANG) $(CXXFLAGS) test_terminal_display.cpp ../TerminalDisplay.cpp ../TerminalController.cpp ../BellPolicy.cpp ../KeypadController.cpp ../BluetoothSync.cpp ../TripRecordCodec.cpp -o $(TEST_BINARY)

coverage: test
	$(LLVM_PROFDATA) merge -sparse $(PROFILE_RAW) -o $(PROFILE_DATA)
	$(LLVM_COV) report $(TEST_BINARY) -instr-profile=$(PROFILE_DATA) ../TerminalDisplay.cpp ../TerminalController.cpp ../KeypadController.cpp ../BluetoothSync.cpp ../TripRecordCodec.cpp

clean:
	rm -rf $(BUILD_DIR)
