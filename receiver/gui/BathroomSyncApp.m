#import <Cocoa/Cocoa.h>

@interface SyncApp : NSObject <NSApplicationDelegate>
@property NSWindow *window;
@property NSTextField *status;
@property NSTextField *count;
@property NSTextView *log;
@property NSButton *sync;
@property NSTask *task;
@property NSPipe *pipe;
@property NSString *csvPath;
@property NSInteger saved;
@end

@implementation SyncApp
- (void)applicationDidFinishLaunching:(NSNotification *)note {
  NSURL *documents = [[NSFileManager defaultManager] URLsForDirectory:NSDocumentDirectory inDomains:NSUserDomainMask].firstObject;
  NSURL *folder = [documents URLByAppendingPathComponent:@"Hallzee" isDirectory:YES];
  [[NSFileManager defaultManager] createDirectoryAtURL:folder withIntermediateDirectories:YES attributes:nil error:nil];
  self.csvPath = [[folder URLByAppendingPathComponent:@"hallzee_trips.csv"] path];
  NSURL *legacy = [[documents URLByAppendingPathComponent:@"Bathroom Terminal" isDirectory:YES] URLByAppendingPathComponent:@"bathroom_trips.csv"];
  if (![[NSFileManager defaultManager] fileExistsAtPath:self.csvPath] && [[NSFileManager defaultManager] fileExistsAtPath:legacy.path]) {
    [[NSFileManager defaultManager] copyItemAtURL:legacy toURL:[NSURL fileURLWithPath:self.csvPath] error:nil];
  }
  [self buildWindow];
}
- (BOOL)applicationShouldTerminateAfterLastWindowClosed:(NSApplication *)sender { return YES; }
- (void)applicationWillTerminate:(NSNotification *)note { [self stop]; }
- (NSTextField *)label:(NSString *)text size:(CGFloat)size {
  NSTextField *field = [NSTextField labelWithString:text];
  field.font = [NSFont systemFontOfSize:size]; field.translatesAutoresizingMaskIntoConstraints = NO; return field;
}
- (void)buildWindow {
  self.window = [[NSWindow alloc] initWithContentRect:NSMakeRect(0,0,700,520) styleMask:(NSWindowStyleMaskTitled|NSWindowStyleMaskClosable|NSWindowStyleMaskMiniaturizable) backing:NSBackingStoreBuffered defer:NO];
  self.window.title = @"Hallzee Sync"; [self.window center];
  NSView *view = self.window.contentView;
  NSTextField *title = [self label:@"Hallzee Sync" size:24]; title.font = [NSFont boldSystemFontOfSize:24]; [view addSubview:title];
  NSTextField *device = [self label:@"Device: Hallzee" size:14]; [view addSubview:device];
  self.status = [self label:@"Ready to sync." size:15]; self.status.textColor = NSColor.systemBlueColor; [view addSubview:self.status];
  self.count = [self label:@"Trips saved this session: 0" size:13]; [view addSubview:self.count];
  NSTextField *path = [self label:[NSString stringWithFormat:@"CSV: %@",self.csvPath] size:12]; path.lineBreakMode=NSLineBreakByTruncatingMiddle; [view addSubview:path];
  self.sync = [NSButton buttonWithTitle:@"Sync Now" target:self action:@selector(toggle:)]; self.sync.translatesAutoresizingMaskIntoConstraints=NO; [view addSubview:self.sync];
  NSButton *open = [NSButton buttonWithTitle:@"Open CSV" target:self action:@selector(openCSV:)]; open.translatesAutoresizingMaskIntoConstraints=NO; [view addSubview:open];
  NSScrollView *scroll = [[NSScrollView alloc] init]; scroll.translatesAutoresizingMaskIntoConstraints=NO; scroll.hasVerticalScroller=YES; scroll.borderType=NSBezelBorder;
  self.log=[[NSTextView alloc]initWithFrame:NSMakeRect(0, 0, 640, 320)];
  self.log.editable=NO;
  self.log.selectable=YES;
  self.log.verticallyResizable=YES;
  self.log.horizontallyResizable=NO;
  self.log.autoresizingMask=NSViewWidthSizable;
  self.log.textContainer.widthTracksTextView=YES;
  self.log.font=[NSFont monospacedSystemFontOfSize:12 weight:NSFontWeightRegular];
  scroll.documentView=self.log; [view addSubview:scroll];
  [NSLayoutConstraint activateConstraints:@[
    [title.topAnchor constraintEqualToAnchor:view.topAnchor constant:24],[title.leadingAnchor constraintEqualToAnchor:view.leadingAnchor constant:24],
    [device.topAnchor constraintEqualToAnchor:title.bottomAnchor constant:12],[device.leadingAnchor constraintEqualToAnchor:title.leadingAnchor],
    [self.status.topAnchor constraintEqualToAnchor:device.bottomAnchor constant:8],[self.status.leadingAnchor constraintEqualToAnchor:title.leadingAnchor],
    [self.count.topAnchor constraintEqualToAnchor:self.status.bottomAnchor constant:8],[self.count.leadingAnchor constraintEqualToAnchor:title.leadingAnchor],
    [path.topAnchor constraintEqualToAnchor:self.count.bottomAnchor constant:8],[path.leadingAnchor constraintEqualToAnchor:title.leadingAnchor],[path.trailingAnchor constraintEqualToAnchor:view.trailingAnchor constant:-24],
    [self.sync.topAnchor constraintEqualToAnchor:path.bottomAnchor constant:16],[self.sync.leadingAnchor constraintEqualToAnchor:title.leadingAnchor],
    [open.centerYAnchor constraintEqualToAnchor:self.sync.centerYAnchor],[open.leadingAnchor constraintEqualToAnchor:self.sync.trailingAnchor constant:10],
    [scroll.topAnchor constraintEqualToAnchor:self.sync.bottomAnchor constant:16],[scroll.leadingAnchor constraintEqualToAnchor:view.leadingAnchor constant:24],[scroll.trailingAnchor constraintEqualToAnchor:view.trailingAnchor constant:-24],[scroll.bottomAnchor constraintEqualToAnchor:view.bottomAnchor constant:-24]]];
  [self.window makeKeyAndOrderFront:nil];
}
- (void)toggle:(id)sender { self.task.isRunning ? [self stop] : [self start]; }
- (void)start {
  NSString *helper=[NSBundle.mainBundle.executablePath.stringByDeletingLastPathComponent stringByAppendingPathComponent:@"bathroom-receiver"];
  if (![[NSFileManager defaultManager] isExecutableFileAtPath:helper]) { [self setStatus:@"Receiver helper is missing. Rebuild the app." color:NSColor.systemRedColor]; return; }
  self.saved=0; self.count.stringValue=@"Trips saved this session: 0"; self.log.string=@""; [self append:@"Starting sync...\n"]; [self setStatus:@"Searching for Hallzee..." color:NSColor.systemBlueColor];
  self.pipe=[NSPipe pipe]; self.task=[[NSTask alloc]init]; self.task.launchPath=helper; self.task.arguments=@[@"Hallzee",self.csvPath]; self.task.standardOutput=self.pipe; self.task.standardError=self.pipe;
  __weak SyncApp *weakSelf=self;
  self.pipe.fileHandleForReading.readabilityHandler=^(NSFileHandle *h){ NSData *d=[h availableData]; if (!d.length) return; NSString *s=[[NSString alloc]initWithData:d encoding:NSUTF8StringEncoding]; dispatch_async(dispatch_get_main_queue(), ^{ [weakSelf handle:s]; }); };
  self.task.terminationHandler=^(NSTask *t){ dispatch_async(dispatch_get_main_queue(), ^{ weakSelf.pipe.fileHandleForReading.readabilityHandler=nil; weakSelf.task=nil; weakSelf.pipe=nil; weakSelf.sync.title=@"Sync Now"; [weakSelf setStatus:t.terminationStatus ? @"Sync stopped or could not connect." : @"Sync closed." color:t.terminationStatus ? NSColor.systemRedColor : NSColor.secondaryLabelColor]; }); };
  [self.task launch]; self.sync.title=@"Stop";
}
- (void)stop { if (self.task.isRunning) { [self append:@"Stopping sync...\n"]; [self.task terminate]; } }
- (void)handle:(NSString *)output {
  [self append:output];
  for (NSString *line in [output componentsSeparatedByString:@"\n"]) {
    if ([line containsString:@"Found requested device"]) [self setStatus:@"Device found. Connecting..." color:NSColor.systemBlueColor];
    else if ([line containsString:@"Connected to"]) [self setStatus:@"Connected. Synchronizing..." color:NSColor.systemGreenColor];
    else if ([line containsString:@"Saved trip "]) { self.saved++; self.count.stringValue=[NSString stringWithFormat:@"Trips saved this session: %ld",(long)self.saved]; }
    else if ([line containsString:@"Requesting full trip history"]) [self setStatus:@"Checking full trip history..." color:NSColor.systemGreenColor];
    else if ([line containsString:@"ESP32: SYNC_END"]) [self setStatus:@"Sync complete." color:NSColor.systemGreenColor];
  }
}
- (void)append:(NSString *)text { [self.log.textStorage appendAttributedString:[[NSAttributedString alloc]initWithString:text]]; [self.log scrollRangeToVisible:NSMakeRange(self.log.string.length,0)]; }
- (void)setStatus:(NSString *)text color:(NSColor *)color { self.status.stringValue=text; self.status.textColor=color; }
- (void)openCSV:(id)sender { if ([[NSFileManager defaultManager] fileExistsAtPath:self.csvPath]) [[NSWorkspace sharedWorkspace] openURL:[NSURL fileURLWithPath:self.csvPath]]; else [self setStatus:@"No CSV yet—run a sync first." color:NSColor.systemOrangeColor]; }
@end
int main(int argc,const char *argv[]) { @autoreleasepool { NSApplication *app=NSApplication.sharedApplication; SyncApp *delegate=[SyncApp new]; app.delegate=delegate; [app run]; } return 0; }
