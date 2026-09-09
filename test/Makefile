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

test: $(TEST_BINARY) $(BUILD_DIR)/touch-tests
	LLVM_PROFILE_FILE=$(PROFILE_RAW) $(TEST_BINARY)
	LLVM_PROFILE_FILE=$(BUILD_DIR)/touch.profraw $(BUILD_DIR)/touch-tests

$(BUILD_DIR)/touch-tests: test_touch_experiment.cpp ../TouchExperiment.cpp ../TouchExperiment.h ../TouchInput.h ../Config.h support/Arduino.h support/touch/Adafruit_ILI9341.h support/touch/XPT2046_Touchscreen.h support/touch/SPI.h
	mkdir -p $(BUILD_DIR)
	$(CLANG) $(CXXFLAGS) -Isupport/touch -DHALLZEE_ILI9341 -DHALLZEE_DISPLAY_ROTATION=1 -DHALLZEE_TOUCH_TEST test_touch_experiment.cpp ../TouchExperiment.cpp -o $@

$(TEST_BINARY): ../TouchInput.h ../FirmwareFrame.h ../TerminalIdentity.h support/Arduino.h support/Preferences.h support/esp_system.h test_terminal_display.cpp ../TerminalIdentity.cpp ../TerminalDisplay.cpp ../TerminalDisplay.h ../TerminalController.cpp ../TerminalController.h ../BellPolicy.cpp ../BellPolicy.h ../KeypadController.cpp ../KeypadController.h ../BluetoothSync.cpp ../BluetoothSync.h ../BluetoothSerialPort.h ../TripRecordCodec.cpp ../TripRecordCodec.h ../KeypadPort.h ../MonotonicClock.h ../TripStoragePort.h ../StudentIdPolicy.h ../TimeProvider.h ../DisplayPort.h ../DisplayColors.h ../Config.h ../AppTypes.h
	mkdir -p $(BUILD_DIR)
	$(CLANG) $(CXXFLAGS) test_terminal_display.cpp ../TerminalIdentity.cpp ../TerminalDisplay.cpp ../TerminalController.cpp ../BellPolicy.cpp ../KeypadController.cpp ../BluetoothSync.cpp ../TripRecordCodec.cpp -o $(TEST_BINARY)

coverage: test
	$(LLVM_PROFDATA) merge -sparse $(PROFILE_RAW) -o $(PROFILE_DATA)
	$(LLVM_COV) report $(TEST_BINARY) -instr-profile=$(PROFILE_DATA) ../TerminalIdentity.cpp ../TerminalDisplay.cpp ../TerminalController.cpp ../KeypadController.cpp ../BluetoothSync.cpp ../TripRecordCodec.cpp

clean:
	rm -rf $(BUILD_DIR)
