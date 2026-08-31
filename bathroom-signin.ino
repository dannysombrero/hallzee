#include <Adafruit_GFX.h>
#include <Adafruit_ST7735.h>
#include <SPI.h>
#include <Keypad.h>
#include "ArduinoKeypadPort.h"
#include "ArduinoBluetoothSerialPort.h"
#include "AppTypes.h"
#include "BluetoothSync.h"
#include "ClockService.h"
#include "Config.h"
#include "KeypadController.h"
#include "MonotonicClock.h"
#include "St7735DisplayPort.h"
#include "TerminalDisplay.h"
#include "TerminalController.h"
#include "TimeProvider.h"
#include "TripStorage.h"
#include <time.h>
#include <sys/time.h>

Adafruit_ST7735 tft = Adafruit_ST7735(TFT_CS, TFT_DC, TFT_MOSI, TFT_SCLK, TFT_RST);
St7735DisplayPort st7735Display(tft);
TerminalDisplay terminalDisplay(st7735Display);

char keys[KEYPAD_ROWS][KEYPAD_COLS] = {{'1','2','3'},{'4','5','6'},{'7','8','9'},{'*','0','#'}};
byte rowPins[KEYPAD_ROWS] = {32, 33, 25, 26};
byte colPins[KEYPAD_COLS] = {27, 14, 13};
Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, KEYPAD_ROWS, KEYPAD_COLS);
ArduinoKeypadPort arduinoKeypad(keypad);
ArduinoMonotonicClock monotonicClock;
TripStorage tripStorage;
ArduinoBluetoothSerialPort bluetoothSerial;
SystemTimeProvider systemTime;
TerminalController terminal(tripStorage, systemTime);
ClockService terminalClock;

void drawIdleScreen();
void setSystemClock24(int year,int month,int day,int hour,int minute,int second);
void handleBluetoothClockSet();
BluetoothSync bluetoothSync(tripStorage, bluetoothSerial, setSystemClock24, handleBluetoothClockSet);
String enteredID = "";
int lastDisplayedMinute = -1;
ClockSetupStep setupStep;
bool setupMode = false;
String setupEntry = "";
int setupMonth=0, setupDay=0, setupYear=0, setupHour=0, setupMinute=0;
bool setupPM=false;

void handleSetupKey(char key); void handleNormalNumber(char key); void handleSingleStar(); void handleSingleHash(); void resetCurrentCheckout();
bool isSetupMode(){return setupMode;} bool isResetAllowed(){return terminal.hasActivePass();}
KeypadController keypadController(arduinoKeypad,monotonicClock,isSetupMode,isResetAllowed,handleSetupKey,handleNormalNumber,handleSingleStar,handleSingleHash,resetCurrentCheckout);

void setSystemClock24(int y,int m,int d,int h,int min,int s){terminalClock.set24Hour(y,m,d,h,min,s);lastDisplayedMinute=-1;}
void setSystemClock(int y,int m,int d,int h,int min,bool pm){terminalClock.set12Hour(y,m,d,h,min,pm);lastDisplayedMinute=-1;}
String getTimeString(){return terminalClock.timeString();} String getDateString(){return terminalClock.dateString();}
int daysInMonth(int m,int y){return ClockService::daysInMonth(m,y);}
void showBluetoothClockSync(){terminalDisplay.showBluetoothClockSynced(getDateString(),getTimeString());}
void handleBluetoothClockSet(){if(!setupMode)return;setupMode=false;setupEntry="";enteredID="";showBluetoothClockSync();drawIdleScreen();}
void drawIDEntry(){terminalDisplay.drawIdEntry(enteredID);} void drawClock(){terminalDisplay.drawClock(getTimeString());}
void updateClockIfNeeded(){if(!terminalClock.isSet())return;time_t now;time(&now);struct tm ti;localtime_r(&now,&ti);if(ti.tm_min!=lastDisplayedMinute){lastDisplayedMinute=ti.tm_min;drawClock();}}
void drawIdleScreen(){terminalDisplay.drawIdleScreen(terminal.activeId(),enteredID);lastDisplayedMinute=-1;updateClockIfNeeded();}
void showCheckedOut(const String&id){terminalDisplay.showCheckedOut(id,getTimeString());} void showCheckedIn(const String&,unsigned long s){terminalDisplay.showCheckedIn(s);}
void showWrongID(){terminalDisplay.showPassOccupied();} void showEnterID(){terminalDisplay.showEnterId();} void showStorageError(){terminalDisplay.showStorageError();}
void showTripLogSummary(){terminalDisplay.showTripLogSummary(tripStorage.isLogReady(),tripStorage.getTripRecordCount(),tripStorage.getLatestTripID());}
void resetCurrentCheckout(){String oldID;if(!terminal.resetActivePass(oldID)){showStorageError();drawIdleScreen();return;}enteredID="";terminalDisplay.showManualReset(oldID);Serial.print("Manual reset. Cleared ID: ");Serial.println(oldID);drawIdleScreen();}
void drawSetupEntry(){terminalDisplay.drawClockSetupEntry(setupEntry);} void drawSetupScreen(){terminalDisplay.drawClockSetupScreen(setupStep,setupEntry);}
void showInvalidValue(const String&m){terminalDisplay.showInvalidClockValue(m);setupEntry="";drawSetupScreen();}
void beginClockSetup(){setupMode=true;setupStep=SET_MONTH;setupEntry="";setupMonth=setupDay=setupYear=setupHour=setupMinute=0;setupPM=false;drawSetupScreen();Serial.println("Clock setup started.");}
void finishClockSetup(){setSystemClock(setupYear,setupMonth,setupDay,setupHour,setupMinute,setupPM);setupMode=false;setupEntry="";terminalDisplay.showClockSet(getDateString(),getTimeString());enteredID="";drawIdleScreen();}
void processSetupEntry(){if(setupEntry.length()==0)return;int v=setupEntry.toInt();switch(setupStep){case SET_MONTH:if(v<1||v>12){showInvalidValue("Month must be 1-12");return;}setupMonth=v;setupStep=SET_DAY;break;case SET_DAY:if(v<1||v>31||(setupMonth==2&&v>29)||((setupMonth==4||setupMonth==6||setupMonth==9||setupMonth==11)&&v>30)){showInvalidValue("Invalid day");return;}setupDay=v;setupStep=SET_YEAR;break;case SET_YEAR:if(v<2024||v>2099){showInvalidValue("Use YYYY: 2024-2099");return;}setupYear=v;if(setupDay>daysInMonth(setupMonth,setupYear)){showInvalidValue("Date does not exist");setupStep=SET_DAY;return;}setupStep=SET_HOUR;break;case SET_HOUR:if(v<1||v>12){showInvalidValue("Hour must be 1-12");return;}setupHour=v;setupStep=SET_MINUTE;break;case SET_MINUTE:if(v<0||v>59){showInvalidValue("Minute must be 0-59");return;}setupMinute=v;setupStep=SET_AMPM;break;case SET_AMPM:if(v!=1&&v!=2){showInvalidValue("1 = AM, 2 = PM");return;}setupPM=(v==2);finishClockSetup();return;}setupEntry="";drawSetupScreen();}
void handleSetupKey(char k){if(k>='0'&&k<='9'){int maxLength=setupStep==SET_YEAR?4:(setupStep==SET_AMPM?1:2);if(setupEntry.length()<maxLength){setupEntry+=k;drawSetupEntry();}return;}if(k=='*'){setupEntry="";drawSetupEntry();return;}if(k=='#')processSetupEntry();}
void submitID(){const String submittedID=enteredID;const TerminalActionResult r=terminal.submit(submittedID);switch(r.action){case TerminalAction::EmptyId:showEnterID();drawIdleScreen();return;case TerminalAction::StartClockSetup:enteredID="";beginClockSetup();return;case TerminalAction::ShowTripLog:enteredID="";showTripLogSummary();drawIdleScreen();return;case TerminalAction::CheckedOut:showCheckedOut(r.id);enteredID="";drawIdleScreen();return;case TerminalAction::CheckedIn:showCheckedIn(r.id,r.elapsedSeconds);enteredID="";drawIdleScreen();return;case TerminalAction::StorageError:enteredID="";showStorageError();drawIdleScreen();return;case TerminalAction::PassOccupied:showWrongID();enteredID="";drawIdleScreen();return;}}
void handleNormalNumber(char k){if(enteredID.length()<MAX_ID_LENGTH){Serial.print("Key pressed: ");Serial.println(k);enteredID+=k;drawIDEntry();}}
void handleSingleStar(){enteredID="";drawIDEntry();} void handleSingleHash(){submitID();}
void processSerialCommands(){while(Serial.available()){char c=(char)Serial.read();if(c=='p'||c=='P')tripStorage.printTripLog();}}
void setup(){Serial.begin(115200);delay(500);Serial.println();Serial.println("Bathroom Terminal Starting...");bluetoothSync.begin();tripStorage.begin();terminal.restoreActivePass();Serial.println("Serial command: p = print trip log");keypadController.begin();tft.initR(INITR_BLACKTAB);tft.setRotation(1);tft.fillScreen(ST77XX_BLACK);if(!terminalClock.isSet())beginClockSetup();else drawIdleScreen();}
void loop(){bluetoothSync.poll();processSerialCommands();keypadController.poll();updateClockIfNeeded();delay(10);}
