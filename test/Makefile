CLANG := xcrun clang++
LLVM_PROFDATA := xcrun llvm-profdata
LLVM_COV := xcrun llvm-cov
BUILD_DIR := build
FIRMWARE_DIR := ../firmware/terminal
TEST_BINARY := $(BUILD_DIR)/native-tests
PROFILE_RAW := $(BUILD_DIR)/default.profraw
PROFILE_DATA := $(BUILD_DIR)/coverage.profdata

CXXFLAGS := -std=c++17 -Wall -Wextra -Werror -I../test/support -I$(FIRMWARE_DIR) \
	-fprofile-instr-generate -fcoverage-mapping

.PHONY: test coverage clean

test: $(TEST_BINARY) $(BUILD_DIR)/touch-tests
	LLVM_PROFILE_FILE=$(PROFILE_RAW) $(TEST_BINARY)
	LLVM_PROFILE_FILE=$(BUILD_DIR)/touch.profraw $(BUILD_DIR)/touch-tests

$(BUILD_DIR)/touch-tests: test_touch_experiment.cpp $(FIRMWARE_DIR)/TouchExperiment.cpp $(FIRMWARE_DIR)/TouchExperiment.h $(FIRMWARE_DIR)/TouchInput.h $(FIRMWARE_DIR)/Config.h support/Arduino.h support/touch/Adafruit_ILI9341.h support/touch/XPT2046_Touchscreen.h support/touch/SPI.h
	mkdir -p $(BUILD_DIR)
	$(CLANG) $(CXXFLAGS) -Isupport/touch -DHALLZEE_ILI9341 -DHALLZEE_DISPLAY_ROTATION=1 -DHALLZEE_TOUCH_TEST test_touch_experiment.cpp $(FIRMWARE_DIR)/TouchExperiment.cpp -o $@

$(TEST_BINARY): $(FIRMWARE_DIR)/StableKeypad.h $(FIRMWARE_DIR)/BondRepair.h $(FIRMWARE_DIR)/TouchInput.h $(FIRMWARE_DIR)/FirmwareFrame.h $(FIRMWARE_DIR)/TerminalIdentity.h support/Arduino.h support/Preferences.h support/esp_system.h test_terminal_display.cpp $(FIRMWARE_DIR)/TerminalIdentity.cpp $(FIRMWARE_DIR)/TerminalDisplay.cpp $(FIRMWARE_DIR)/TerminalDisplay.h $(FIRMWARE_DIR)/TerminalController.cpp $(FIRMWARE_DIR)/TerminalController.h $(FIRMWARE_DIR)/BellPolicy.cpp $(FIRMWARE_DIR)/BellPolicy.h $(FIRMWARE_DIR)/KeypadController.cpp $(FIRMWARE_DIR)/KeypadController.h $(FIRMWARE_DIR)/BluetoothSync.cpp $(FIRMWARE_DIR)/BluetoothSync.h $(FIRMWARE_DIR)/BluetoothSerialPort.h $(FIRMWARE_DIR)/TripRecordCodec.cpp $(FIRMWARE_DIR)/TripRecordCodec.h $(FIRMWARE_DIR)/KeypadPort.h $(FIRMWARE_DIR)/MonotonicClock.h $(FIRMWARE_DIR)/TripStoragePort.h $(FIRMWARE_DIR)/StudentIdPolicy.h $(FIRMWARE_DIR)/TimeProvider.h $(FIRMWARE_DIR)/DisplayPort.h $(FIRMWARE_DIR)/DisplayColors.h $(FIRMWARE_DIR)/Config.h $(FIRMWARE_DIR)/AppTypes.h
	mkdir -p $(BUILD_DIR)
	$(CLANG) $(CXXFLAGS) test_terminal_display.cpp $(FIRMWARE_DIR)/TerminalIdentity.cpp $(FIRMWARE_DIR)/TerminalDisplay.cpp $(FIRMWARE_DIR)/TerminalController.cpp $(FIRMWARE_DIR)/BellPolicy.cpp $(FIRMWARE_DIR)/KeypadController.cpp $(FIRMWARE_DIR)/BluetoothSync.cpp $(FIRMWARE_DIR)/TripRecordCodec.cpp -o $(TEST_BINARY)

coverage: test
	$(LLVM_PROFDATA) merge -sparse $(PROFILE_RAW) -o $(PROFILE_DATA)
	$(LLVM_COV) report $(TEST_BINARY) -instr-profile=$(PROFILE_DATA) $(FIRMWARE_DIR)/TerminalIdentity.cpp $(FIRMWARE_DIR)/TerminalDisplay.cpp $(FIRMWARE_DIR)/TerminalController.cpp $(FIRMWARE_DIR)/KeypadController.cpp $(FIRMWARE_DIR)/BluetoothSync.cpp $(FIRMWARE_DIR)/TripRecordCodec.cpp

clean:
	rm -rf $(BUILD_DIR)
