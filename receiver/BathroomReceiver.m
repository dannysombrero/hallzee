#import <Foundation/Foundation.h>
#import <IOBluetooth/IOBluetooth.h>

@interface BathroomReceiver : NSObject <IOBluetoothRFCOMMChannelDelegate>

@property(nonatomic, strong) IOBluetoothDevice *device;
@property(nonatomic, strong) IOBluetoothRFCOMMChannel *channel;
@property(nonatomic, strong) NSMutableData *incomingData;
@property(nonatomic, strong) NSMutableSet<NSString *> *knownTripIDs;
@property(nonatomic, copy) NSString *csvPath;
@property(nonatomic, assign) BOOL sdpQueryFinished;
@property(nonatomic, assign) IOReturn sdpQueryStatus;
@property(nonatomic, assign) BOOL requestFullHistoryAfterInitialSync;

- (instancetype)initWithDevice:(IOBluetoothDevice *)device csvPath:(NSString *)csvPath;
- (BOOL)connect;

@end

@interface NearbyDeviceFinder : NSObject <IOBluetoothDeviceInquiryDelegate>

@property(nonatomic, copy) NSString *deviceName;
@property(nonatomic, strong) IOBluetoothDevice *foundDevice;
@property(nonatomic, assign) BOOL inquiryFinished;
@property(nonatomic, assign) IOReturn inquiryStatus;

- (instancetype)initWithDeviceName:(NSString *)deviceName;
- (IOBluetoothDevice *)findDevice;

@end

@implementation NearbyDeviceFinder

- (instancetype)initWithDeviceName:(NSString *)deviceName {
  self = [super init];

  if (self) {
    _deviceName = [deviceName copy];
    _inquiryStatus = kIOReturnError;
  }

  return self;
}

- (IOBluetoothDevice *)findDevice {
  IOBluetoothDeviceInquiry *inquiry = [IOBluetoothDeviceInquiry inquiryWithDelegate:self];
  inquiry.inquiryLength = 8;  // About ten seconds.

  IOReturn startStatus = [inquiry start];
  fprintf(stdout, "Bluetooth discovery start status: 0x%08x\n", startStatus);
  fflush(stdout);

  if (startStatus != kIOReturnSuccess) {
    fprintf(stderr, "Could not start Bluetooth discovery (IOReturn 0x%08x).\n", startStatus);
    return nil;
  }

  NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:15.0];

  while (!self.inquiryFinished && !self.foundDevice) {
    if ([deadline timeIntervalSinceNow] <= 0) {
      fprintf(stderr, "Bluetooth discovery timed out after 15 seconds.\n");
      [inquiry stop];
      break;
    }

    [[NSRunLoop currentRunLoop] runMode:NSDefaultRunLoopMode
                             beforeDate:[NSDate dateWithTimeIntervalSinceNow:0.1]];
  }

  if (self.foundDevice == nil) {
    fprintf(stderr, "Bluetooth discovery ended: status 0x%08x.\n", self.inquiryStatus);
  }

  return self.foundDevice;
}

- (void)deviceInquiryDeviceFound:(IOBluetoothDeviceInquiry *)sender
                          device:(IOBluetoothDevice *)device {
  NSString *visibleName = device.nameOrAddress ?: @"(unnamed)";
  NSString *visibleAddress = device.addressString ?: @"(no address)";
  fprintf(stdout, "Found nearby Bluetooth device: %s (%s)\n",
          visibleName.UTF8String,
          visibleAddress.UTF8String);
  fflush(stdout);

  if ([device.name isEqualToString:self.deviceName] ||
      [device.nameOrAddress isEqualToString:self.deviceName]) {
    fprintf(stdout, "Found requested device. Opening its serial service...\n");
    fflush(stdout);
    self.foundDevice = device;
    [sender stop];
  }
}

- (void)deviceInquiryComplete:(IOBluetoothDeviceInquiry *)sender
                         error:(IOReturn)error
                       aborted:(BOOL)aborted {
  self.inquiryStatus = error;
  self.inquiryFinished = YES;
  fprintf(stdout, "Bluetooth discovery complete: status 0x%08x, aborted=%s.\n",
          error, aborted ? "yes" : "no");
  fflush(stdout);
}

@end

@implementation BathroomReceiver

- (instancetype)initWithDevice:(IOBluetoothDevice *)device csvPath:(NSString *)csvPath {
  self = [super init];

  if (self) {
    _device = device;
    _csvPath = [csvPath copy];
    _incomingData = [NSMutableData data];
    _knownTripIDs = [NSMutableSet set];
    [self prepareCSV];
  }

  return self;
}

- (void)prepareCSV {
  NSFileManager *fileManager = [NSFileManager defaultManager];

  if (![fileManager fileExistsAtPath:self.csvPath]) {
    NSData *header = [@"trip_id,student_id,date,time_out,time_in,duration_seconds,status\n"
      dataUsingEncoding:NSUTF8StringEncoding];
    [fileManager createFileAtPath:self.csvPath contents:header attributes:nil];
    return;
  }

  if (![self sortCSVByTripID]) {
    NSLog(@"WARNING: Could not sort the existing CSV. New trip rows remain safe.");
  }

  NSError *error = nil;
  NSString *contents = [NSString stringWithContentsOfFile:self.csvPath
                                                 encoding:NSUTF8StringEncoding
                                                    error:&error];

  if (error) {
    NSLog(@"WARNING: Could not read existing CSV (%@). Duplicate protection starts empty.", error);
    return;
  }

  for (NSString *line in [contents componentsSeparatedByString:@"\n"]) {
    NSArray<NSString *> *columns = [line componentsSeparatedByString:@","];

    if (columns.count >= 1 && [columns[0] longLongValue] > 0) {
      [self.knownTripIDs addObject:columns[0]];
    }
  }
}

- (BOOL)sortCSVByTripID {
  NSError *readError = nil;
  NSString *contents = [NSString stringWithContentsOfFile:self.csvPath
                                                 encoding:NSUTF8StringEncoding
                                                    error:&readError];

  if (readError != nil) {
    return NO;
  }

  NSArray<NSString *> *lines = [contents componentsSeparatedByString:@"\n"];
  NSString *header = nil;
  NSMutableArray<NSString *> *rows = [NSMutableArray array];

  for (NSString *line in lines) {
    if (line.length == 0) {
      continue;
    }

    if (header == nil) {
      header = line;
    } else {
      [rows addObject:line];
    }
  }

  if (header == nil) {
    return NO;
  }

  [rows sortUsingComparator:^NSComparisonResult(NSString *left, NSString *right) {
    long long leftID = [[left componentsSeparatedByString:@","][0] longLongValue];
    long long rightID = [[right componentsSeparatedByString:@","][0] longLongValue];

    if (leftID < rightID) {
      return NSOrderedAscending;
    }

    if (leftID > rightID) {
      return NSOrderedDescending;
    }

    return [left compare:right];
  }];

  NSString *sortedContents = [NSString stringWithFormat:@"%@\n", header];

  if (rows.count > 0) {
    sortedContents = [sortedContents stringByAppendingString:
      [[rows componentsJoinedByString:@"\n"] stringByAppendingString:@"\n"]];
  }

  NSError *writeError = nil;
  BOOL wroteFile = [sortedContents writeToFile:self.csvPath
                                    atomically:YES
                                      encoding:NSUTF8StringEncoding
                                         error:&writeError];

  if (!wroteFile) {
    NSLog(@"CSV sort write failed: %@", writeError);
  }

  return wroteFile;
}

- (BOOL)connect {
  self.sdpQueryFinished = NO;
  self.sdpQueryStatus = kIOReturnError;

  if (!self.device.isConnected) {
    fprintf(stdout, "Opening Bluetooth connection to %s...\n", self.device.nameOrAddress.UTF8String);
    fflush(stdout);
    IOReturn connectionStatus = [self.device openConnection];
    fprintf(stdout, "Bluetooth connection status: 0x%08x\n", connectionStatus);
    fflush(stdout);

    if (connectionStatus != kIOReturnSuccess) {
      fprintf(stderr, "Could not open a Bluetooth connection (IOReturn 0x%08x).\n", connectionStatus);
      return NO;
    }
  }

  fprintf(stdout, "Looking up the ESP32 serial service...\n");
  fflush(stdout);
  IOReturn queryStart = [self.device performSDPQuery:self];

  if (queryStart != kIOReturnSuccess) {
    fprintf(stderr, "Could not start SPP service discovery (IOReturn 0x%08x).\n", queryStart);
    return NO;
  }

  NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:15.0];

  while (!self.sdpQueryFinished) {
    if ([deadline timeIntervalSinceNow] <= 0) {
      fprintf(stderr, "SPP service discovery timed out after 15 seconds.\n");
      return NO;
    }

    [[NSRunLoop currentRunLoop] runMode:NSDefaultRunLoopMode
                             beforeDate:[NSDate dateWithTimeIntervalSinceNow:0.1]];
  }

  if (self.sdpQueryStatus != kIOReturnSuccess) {
    fprintf(stderr, "SPP service discovery failed (IOReturn 0x%08x).\n", self.sdpQueryStatus);
    return NO;
  }

  BluetoothRFCOMMChannelID channelID = 0;
  BOOL foundChannel = NO;

  for (IOBluetoothSDPServiceRecord *service in self.device.services) {
    if ([service getRFCOMMChannelID:&channelID] == kIOReturnSuccess) {
      foundChannel = YES;
      break;
    }
  }

  if (!foundChannel) {
    fprintf(stderr, "No RFCOMM/SPP service was found on %s.\n", self.device.nameOrAddress.UTF8String);
    return NO;
  }

  IOBluetoothRFCOMMChannel *openedChannel = nil;
  IOReturn openStatus = [self.device openRFCOMMChannelSync:&openedChannel
                                              withChannelID:channelID
                                                   delegate:self];

  if (openStatus != kIOReturnSuccess || openedChannel == nil) {
    fprintf(stderr, "Could not open SPP channel %u (IOReturn 0x%08x).\n", channelID, openStatus);
    return NO;
  }

  self.channel = openedChannel;
  NSLog(@"Connected to %@. Writing synchronized trips to %@.",
        self.device.nameOrAddress, self.csvPath);
  [self sendCurrentTime];

  return YES;
}

- (void)sdpQueryComplete:(IOBluetoothDevice *)device status:(IOReturn)status {
  self.sdpQueryStatus = status;
  self.sdpQueryFinished = YES;
}

- (void)rfcommChannelData:(IOBluetoothRFCOMMChannel *)rfcommChannel
                      data:(void *)dataPointer
                    length:(size_t)dataLength {
  [self.incomingData appendBytes:dataPointer length:dataLength];

  const uint8_t newline = '\n';

  while (true) {
    NSRange range = [self.incomingData rangeOfData:[NSData dataWithBytes:&newline length:1]
                                           options:0
                                             range:NSMakeRange(0, self.incomingData.length)];

    if (range.location == NSNotFound) {
      break;
    }

    NSData *lineData = [self.incomingData subdataWithRange:NSMakeRange(0, range.location)];
    [self.incomingData replaceBytesInRange:NSMakeRange(0, range.location + 1)
                                 withBytes:NULL
                                    length:0];

    NSString *line = [[NSString alloc] initWithData:lineData encoding:NSUTF8StringEncoding];
    [self handleLine:[line stringByTrimmingCharactersInSet:[NSCharacterSet newlineCharacterSet]]];
  }
}

- (void)rfcommChannelClosed:(IOBluetoothRFCOMMChannel *)rfcommChannel {
  NSLog(@"Bluetooth connection closed. The ESP32 retains any unacknowledged trips.");
  CFRunLoopStop(CFRunLoopGetCurrent());
}

- (void)sendLine:(NSString *)line {
  if (self.channel == nil) {
    return;
  }

  NSData *data = [[line stringByAppendingString:@"\n"] dataUsingEncoding:NSUTF8StringEncoding];
  IOReturn status = [self.channel writeSync:(void *)data.bytes length:(UInt16)data.length];

  if (status != kIOReturnSuccess) {
    NSLog(@"Bluetooth write failed for '%@' (IOReturn 0x%08x).", line, status);
  }
}

- (void)sendCurrentTime {
  NSDateFormatter *formatter = [[NSDateFormatter alloc] init];
  formatter.locale = [NSLocale localeWithLocaleIdentifier:@"en_US_POSIX"];
  formatter.timeZone = [NSTimeZone localTimeZone];
  formatter.dateFormat = @"yyyy-MM-dd,HH:mm:ss";

  self.requestFullHistoryAfterInitialSync = YES;
  [self sendLine:[NSString stringWithFormat:@"TIME,%@", [formatter stringFromDate:[NSDate date]]]];
}

- (BOOL)appendTripColumns:(NSArray<NSString *> *)columns {
  if (columns.count != 8) {
    return NO;
  }

  NSString *csvLine = [NSString stringWithFormat:@"%@,%@,%@,%@,%@,%@,%@\n",
                       columns[0], columns[1], columns[2], columns[3],
                       columns[4], columns[5], columns[6]];

  NSFileHandle *file = [NSFileHandle fileHandleForWritingAtPath:self.csvPath];

  if (file == nil) {
    NSLog(@"CSV is not writable. The trip will remain queued on the ESP32.");
    return NO;
  }

  @try {
    [file seekToEndOfFile];
    [file writeData:[csvLine dataUsingEncoding:NSUTF8StringEncoding]];
    [file synchronizeFile];
    [file closeFile];
  } @catch (NSException *exception) {
    NSLog(@"CSV write failed: %@. The trip will remain queued on the ESP32.", exception.reason);
    [file closeFile];
    return NO;
  }

  return YES;
}

- (void)handleTrip:(NSString *)record {
  NSArray<NSString *> *columns = [record componentsSeparatedByString:@","];

  if (columns.count != 8 || [columns[0] longLongValue] <= 0) {
    NSLog(@"Ignoring malformed trip record: %@", record);
    return;
  }

  NSString *tripID = columns[0];

  if (![self.knownTripIDs containsObject:tripID]) {
    if (![self appendTripColumns:columns]) {
      return;
    }

    [self.knownTripIDs addObject:tripID];
    NSLog(@"Saved trip %@ to CSV.", tripID);
  } else {
    NSLog(@"Trip %@ is already in CSV; acknowledging duplicate safely.", tripID);
  }

  [self sendLine:[NSString stringWithFormat:@"ACK,%@", tripID]];
}

- (void)handleLine:(NSString *)line {
  if (line.length == 0) {
    return;
  }

  NSLog(@"ESP32: %@", line);

  if ([line hasPrefix:@"TRIP,"]) {
    [self handleTrip:[line substringFromIndex:5]];
  } else if ([line isEqualToString:@"SYNC_END"] && self.requestFullHistoryAfterInitialSync) {
    self.requestFullHistoryAfterInitialSync = NO;
    NSLog(@"Requesting full trip history from the ESP32.");
    [self sendLine:@"SYNC_ALL"];
  }
}

@end

int main(int argc, const char * argv[]) {
  @autoreleasepool {
    NSString *deviceName = (argc >= 2) ? [NSString stringWithUTF8String:argv[1]] : @"Hallzee";
    NSString *csvPath = (argc >= 3) ? [NSString stringWithUTF8String:argv[2]] : @"hallzee_trips.csv";

    fprintf(stdout, "Looking for Bluetooth device '%s'...\n", deviceName.UTF8String);
    fflush(stdout);

    fprintf(stdout, "Searching nearby devices for about 15 seconds...\n");
    fflush(stdout);
    NearbyDeviceFinder *finder = [[NearbyDeviceFinder alloc] initWithDeviceName:deviceName];
    IOBluetoothDevice *targetDevice = [finder findDevice];

    if (targetDevice == nil) {
      NSLog(@"Could not find '%@'. Confirm the ESP32 is powered, Android is disconnected, and try again.", deviceName);
      return 1;
    }

    BathroomReceiver *receiver = [[BathroomReceiver alloc] initWithDevice:targetDevice csvPath:csvPath];

    if (![receiver connect]) {
      return 1;
    }

    [[NSRunLoop currentRunLoop] run];
  }

  return 0;
}
