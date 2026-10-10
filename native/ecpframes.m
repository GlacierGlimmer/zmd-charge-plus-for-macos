#import <AppKit/AppKit.h>
#include <unistd.h>
#include <math.h>
#import <ScreenCaptureKit/ScreenCaptureKit.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreGraphics/CoreGraphics.h>

API_AVAILABLE(macos(12.3))
@interface ECPFrameProbe : NSObject <SCStreamOutput,SCStreamDelegate>
@property(nonatomic,strong) SCStream *stream;
@property(nonatomic,strong) NSMutableArray<NSNumber *> *timestamps;
@property(nonatomic,strong) NSMutableArray<NSNumber *> *arrivals;
@property(nonatomic,strong) NSLock *lock;
@property(nonatomic,copy) NSString *status;
@property(nonatomic,copy) NSString *name;
@property BOOL primed;
@property BOOL stopped;
@property double lastDemand;
@end
@implementation ECPFrameProbe
- (instancetype)init { if ((self=[super init])) { _timestamps=[NSMutableArray new]; _arrivals=[NSMutableArray new]; _lock=[NSLock new]; _status=@"等待帧样本 / Waiting for frames"; _name=@""; } return self; }
- (void)stream:(SCStream *)stream didOutputSampleBuffer:(CMSampleBufferRef)sample ofType:(SCStreamOutputType)type {
    (void)stream;
    if (type!=SCStreamOutputTypeScreen || !CMSampleBufferIsValid(sample)) return;
    CFArrayRef attachments=CMSampleBufferGetSampleAttachmentsArray(sample,NO);
    if (!attachments || CFArrayGetCount(attachments)==0) return;
    NSDictionary *info=(__bridge NSDictionary *)CFArrayGetValueAtIndex(attachments,0);
    NSNumber *frameStatus=info[SCStreamFrameInfoStatus];
    if (!frameStatus || frameStatus.integerValue!=SCFrameStatusComplete) return;
    double time=CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp(sample));
    if (!isfinite(time)) return;
    [self.lock lock];
    self.primed=YES; self.status=@"OK";
    if (!self.timestamps.count || time!=self.timestamps.lastObject.doubleValue) {
        [self.timestamps addObject:@(time)]; [self.arrivals addObject:@(NSProcessInfo.processInfo.systemUptime)];
    }
    while (self.timestamps.count>1 && self.arrivals.lastObject.doubleValue-self.arrivals.firstObject.doubleValue>2) { [self.timestamps removeObjectAtIndex:0]; [self.arrivals removeObjectAtIndex:0]; }
    [self.lock unlock];
}
- (void)stream:(SCStream *)stream didStopWithError:(NSError *)error {
    (void)stream;
    [self.lock lock]; self.status=error.localizedDescription; self.primed=NO; [self.timestamps removeAllObjects]; [self.arrivals removeAllObjects]; [self.lock unlock];
}
@end

static NSMutableDictionary<NSString *,ECPFrameProbe *> *probes;
static NSLock *probeLock;
static dispatch_source_t frameWatchdog;
static void stopProbe(ECPFrameProbe *probe) {
    probe.stopped=YES; [probe.stream stopCaptureWithCompletionHandler:^(NSError *error){ (void)error; }];
}
static void frameInit(void) {
    static dispatch_once_t once;
    dispatch_once(&once,^{
        probes=[NSMutableDictionary new]; probeLock=[NSLock new];
        frameWatchdog=dispatch_source_create(DISPATCH_SOURCE_TYPE_TIMER,0,0,dispatch_get_global_queue(QOS_CLASS_UTILITY,0));
        dispatch_source_set_timer(frameWatchdog,dispatch_time(DISPATCH_TIME_NOW,2*NSEC_PER_SEC),2*NSEC_PER_SEC,NSEC_PER_SEC/4);
        dispatch_source_set_event_handler(frameWatchdog,^{
            double now=NSProcessInfo.processInfo.systemUptime;
            [probeLock lock];
            for(NSString *key in probes.allKeys) if(now-probes[key].lastDemand>6) { stopProbe(probes[key]); [probes removeObjectForKey:key]; }
            [probeLock unlock];
        });
        dispatch_resume(frameWatchdog);
    });
}
static char *frameJson(id value) { NSData *data=[NSJSONSerialization dataWithJSONObject:value options:0 error:nil]; return data ? strdup([[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding].UTF8String) : strdup("{}"); }

__attribute__((visibility("default"))) char *ecp_frame_targets(int windows) {
    @autoreleasepool {
        NSMutableArray *targets=[NSMutableArray new];
        if (windows) {
            CFArrayRef items=CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly|kCGWindowListExcludeDesktopElements,kCGNullWindowID);
            for (NSDictionary *item in (__bridge NSArray *)items) {
                if ([item[(__bridge NSString *)kCGWindowOwnerPID] intValue]==getpid() || [item[(__bridge NSString *)kCGWindowLayer] intValue]!=0) continue;
                NSNumber *number=item[(__bridge NSString *)kCGWindowNumber];
                NSString *title=item[(__bridge NSString *)kCGWindowName];
                NSString *owner=item[(__bridge NSString *)kCGWindowOwnerName];
                if (number && owner) [targets addObject:@{@"id":number.stringValue,@"name":[NSString stringWithFormat:@"%@ · %@",owner,title ?: @""]}];
            }
            if(items) CFRelease(items);
        } else {
            CGDirectDisplayID ids[32]; uint32_t count=0;
            if (CGGetActiveDisplayList(32,ids,&count)==kCGErrorSuccess)
                for(uint32_t i=0;i<count;i++) [targets addObject:@{@"id":[NSString stringWithFormat:@"%u",ids[i]],@"name":[NSString stringWithFormat:@"Display %u · %zux%zu",i+1,CGDisplayPixelsWide(ids[i]),CGDisplayPixelsHigh(ids[i])]}];
        }
        return frameJson(targets);
    }
}

static void startProbe(ECPFrameProbe *probe,BOOL window,uint32_t identity) API_AVAILABLE(macos(12.3));
static void startProbe(ECPFrameProbe *probe,BOOL window,uint32_t identity) {
    [SCShareableContent getShareableContentExcludingDesktopWindows:YES onScreenWindowsOnly:YES completionHandler:^(SCShareableContent *content,NSError *error) {
        if (probe.stopped) return;
        if(error) { probe.status=error.localizedDescription; return; }
        SCContentFilter *filter=nil;
        if(window) {
            for(SCWindow *candidate in content.windows) if(candidate.windowID==identity) {
                filter=[[SCContentFilter alloc] initWithDesktopIndependentWindow:candidate]; probe.name=candidate.title ?: candidate.owningApplication.applicationName; break;
            }
        } else {
            for(SCDisplay *candidate in content.displays) if(candidate.displayID==identity) {
                NSMutableArray *exclude=[NSMutableArray new]; for(SCRunningApplication *app in content.applications) if(app.processID==getpid()) [exclude addObject:app];
                filter=[[SCContentFilter alloc] initWithDisplay:candidate excludingApplications:exclude exceptingWindows:@[]]; probe.name=[NSString stringWithFormat:@"Display %u",identity]; break;
            }
        }
        if(!filter) { probe.status=@"所选窗口或显示器已关闭 / Selected target is unavailable"; return; }
        SCStreamConfiguration *config=[SCStreamConfiguration new]; config.width=64; config.height=64;
        config.minimumFrameInterval=CMTimeMake(1,1000); config.queueDepth=3; config.showsCursor=NO;
        SCStream *stream=[[SCStream alloc] initWithFilter:filter configuration:config delegate:probe];
        NSError *outputError=nil;
        if(![stream addStreamOutput:probe type:SCStreamOutputTypeScreen sampleHandlerQueue:dispatch_get_global_queue(QOS_CLASS_UTILITY,0) error:&outputError]) { probe.status=outputError.localizedDescription; return; }
        probe.stream=stream;
        [stream startCaptureWithCompletionHandler:^(NSError *startError) { if(startError) probe.status=startError.localizedDescription; }];
    }];
}

__attribute__((visibility("default"))) char *ecp_frame_read(int window,const char *id) {
    @autoreleasepool {
        if (@available(macOS 12.3,*)) {
            frameInit(); NSString *identity=id ? [NSString stringWithUTF8String:id] : @"";
            if(!identity.length && !window) identity=[NSString stringWithFormat:@"%u",CGMainDisplayID()];
            if(!identity.length) return frameJson(@{@"status":@"请选择窗口 / Select a window"});
            if(!CGPreflightScreenCaptureAccess()) {
                // A frame-rate profile is an explicit request for this capability. The OS owns the consent prompt.
                static dispatch_once_t permissionPrompt;
                dispatch_once(&permissionPrompt,^{ dispatch_async(dispatch_get_main_queue(),^{ CGRequestScreenCaptureAccess(); }); });
                return frameJson(@{@"status":@"需要屏幕录制权限；授权后重新启动应用 / Screen Recording permission required; relaunch after granting"});
            }
            NSString *key=[NSString stringWithFormat:@"%d:%@",window,identity]; double now=NSProcessInfo.processInfo.systemUptime;
            [probeLock lock];
            for(NSString *oldKey in probes.allKeys) {
                ECPFrameProbe *old=probes[oldKey];
                if(now-old.lastDemand>6) { stopProbe(old); [probes removeObjectForKey:oldKey]; }
            }
            ECPFrameProbe *probe=probes[key];
            if(!probe) { probe=[ECPFrameProbe new]; probes[key]=probe; startProbe(probe,window!=0,identity.intValue); }
            probe.lastDemand=now; [probeLock unlock];
            [probe.lock lock];
            NSMutableDictionary *result=[@{@"name":probe.name ?: identity,@"status":probe.status ?: @"Unavailable"} mutableCopy];
            // Presentation timestamps use the monotonic host clock. Idle frames are explicitly excluded.
            while(probe.timestamps.count && now-probe.arrivals.firstObject.doubleValue>2) { [probe.timestamps removeObjectAtIndex:0]; [probe.arrivals removeObjectAtIndex:0]; }
            if(probe.primed && probe.timestamps.count==0) result[@"fps"]=@0;
            else if(probe.timestamps.count>1) { double span=probe.timestamps.lastObject.doubleValue-probe.timestamps.firstObject.doubleValue; if(span>0) result[@"fps"]=@((probe.timestamps.count-1)/span); }
            [probe.lock unlock]; return frameJson(result);
        }
        return frameJson(@{@"status":@"需要 macOS 12.3 或以上 / Requires macOS 12.3 or newer"});
    }
}
__attribute__((visibility("default"))) void ecp_frames_stop(void) {
    if (@available(macOS 12.3,*)) { frameInit(); [probeLock lock]; for(ECPFrameProbe *probe in probes.allValues) { stopProbe(probe); } [probes removeAllObjects]; [probeLock unlock]; }
}

__attribute__((visibility("default"))) void ecp_frames_keepalive(int window,const char *id) {
    @autoreleasepool { frameInit(); NSString *identity=id ? [NSString stringWithUTF8String:id] : @"";
        if(!identity.length && !window) identity=[NSString stringWithFormat:@"%u",CGMainDisplayID()];
        NSString *key=[NSString stringWithFormat:@"%d:%@",window,identity];
        [probeLock lock]; ECPFrameProbe *probe=probes[key]; if(probe) probe.lastDemand=NSProcessInfo.processInfo.systemUptime; [probeLock unlock];
    }
}
