// Public macOS APIs only. Missing measurements are omitted, never fabricated.
#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#import <Metal/Metal.h>
#import <Security/Security.h>
#import <IOKit/ps/IOPowerSources.h>
#import <IOKit/ps/IOPSKeys.h>
#include <mach/mach.h>
#include <sys/sysctl.h>
#include <sys/mount.h>
#include <sys/socket.h>
#include <net/route.h>
#include <net/if.h>
#include <net/if_dl.h>
#include <ifaddrs.h>
#include <arpa/inet.h>

#define API __attribute__((visibility("default")))
static NSNumber *number(const char *key) {
    uint64_t value = 0; size_t size = sizeof(value);
    if (sysctlbyname(key, &value, &size, NULL, 0) != 0) return nil;
    return @(value);
}
static NSString *string(const char *key) {
    char value[1024]; size_t size = sizeof(value);
    if (sysctlbyname(key, value, &size, NULL, 0) != 0 || size == 0) return nil;
    value[sizeof(value)-1] = 0;
    return [NSString stringWithUTF8String:value];
}
static char *json(id value) {
    NSData *data = [NSJSONSerialization dataWithJSONObject:value options:0 error:nil];
    if (!data) return NULL;
    char *out = malloc(data.length + 1);
    if (!out) return NULL;
    memcpy(out, data.bytes, data.length); out[data.length] = 0;
    return out;
}
API void ecp_free(void *value) { free(value); }

static void cpu(NSMutableDictionary *v) {
    NSString *name = string("machdep.cpu.brand_string");
    if (name.length) v[@"cpu.name"] = name;
    NSNumber *cores = number("hw.physicalcpu"), *logical = number("hw.logicalcpu");
    if (cores) v[@"cpu.physical_cores"] = cores;
    if (logical) v[@"cpu.logical_processors"] = logical;
    processor_info_array_t ticks = NULL;
    mach_msg_type_number_t count = 0; natural_t processors = 0;
    mach_port_t host = mach_host_self();
    if (host_processor_info(host, PROCESSOR_CPU_LOAD_INFO, &processors, &ticks, &count) == KERN_SUCCESS) {
        uint32_t totals[CPU_STATE_MAX] = {0};
        for (natural_t i=0; i<processors; i++)
            for (int state=0; state<CPU_STATE_MAX; state++) totals[state] += (uint32_t)ticks[i*CPU_STATE_MAX+state];
        v[@"__cpu_ticks"] = @[@(totals[CPU_STATE_USER]), @(totals[CPU_STATE_SYSTEM]),
                              @(totals[CPU_STATE_IDLE]), @(totals[CPU_STATE_NICE])];
        vm_deallocate(mach_task_self(), (vm_address_t)ticks, count*sizeof(integer_t));
    }
    mach_port_deallocate(mach_task_self(), host);
    // hw.cpufrequency is nominal on some Macs; it is deliberately NOT labeled current GHz.
    struct timeval boot; size_t size = sizeof(boot);
    if (sysctlbyname("kern.boottime", &boot, &size, NULL, 0) == 0) {
        NSDate *date = [NSDate dateWithTimeIntervalSince1970:boot.tv_sec];
        v[@"system.uptime_seconds"] = @(-date.timeIntervalSinceNow);
        NSDateFormatter *format = [NSDateFormatter new];
        format.locale = [NSLocale localeWithLocaleIdentifier:@"en_US_POSIX"];
        format.dateFormat = @"yyyy-MM-dd HH:mm:ss";
        v[@"system.boot_time"] = [format stringFromDate:date];
    }
    NSOperatingSystemVersion version = NSProcessInfo.processInfo.operatingSystemVersion;
    v[@"system.os_version"] = [NSString stringWithFormat:@"%ld.%ld.%ld", (long)version.majorVersion, (long)version.minorVersion, (long)version.patchVersion];
    v[@"system.os_description"] = [@"macOS " stringByAppendingString:v[@"system.os_version"]];
}

static void memory(NSMutableDictionary *v) {
    NSNumber *total = number("hw.memsize");
    vm_statistics64_data_t vm; mach_msg_type_number_t count = HOST_VM_INFO64_COUNT;
    mach_port_t host = mach_host_self(); vm_size_t page;
    if (total && host_page_size(host, &page) == KERN_SUCCESS &&
        host_statistics64(host, HOST_VM_INFO64, (host_info64_t)&vm, &count) == KERN_SUCCESS) {
        double bytes = total.doubleValue;
        // Used = active + wired + compressed. Inactive/file cache remains reclaimable.
        double used = MIN(bytes, ((double)vm.active_count + vm.wire_count + vm.compressor_page_count) * page);
        v[@"memory.total_bytes"] = total;
        v[@"memory.used_bytes"] = @(used); v[@"memory.available_bytes"] = @(MAX(0, bytes-used));
        v[@"memory.usage"] = @(used / bytes * 100);
        v[@"memory.free_bytes"] = @((double)vm.free_count * page);
        v[@"memory.active_bytes"] = @((double)vm.active_count * page);
        v[@"memory.inactive_bytes"] = @((double)vm.inactive_count * page);
        v[@"memory.wired_bytes"] = @((double)vm.wire_count * page);
        v[@"memory.compressed_bytes"] = @((double)vm.compressor_page_count * page);
        v[@"memory.page_size"] = @(page);
    }
    mach_port_deallocate(mach_task_self(), host);
    struct xsw_usage swap; size_t size = sizeof(swap);
    if (sysctlbyname("vm.swapusage", &swap, &size, NULL, 0) == 0) {
        v[@"memory.swap_total_bytes"] = @(swap.xsu_total);
        v[@"memory.swap_used_bytes"] = @(swap.xsu_used);
        v[@"memory.swap_available_bytes"] = @(swap.xsu_avail);
    }
}

static void battery(NSMutableDictionary *v) {
    CFTypeRef info = IOPSCopyPowerSourcesInfo(); if (!info) return;
    CFArrayRef sources = IOPSCopyPowerSourcesList(info);
    if (sources) for (CFIndex i=0; i<CFArrayGetCount(sources); i++) {
        NSDictionary *d = (__bridge NSDictionary *)IOPSGetPowerSourceDescription(info, CFArrayGetValueAtIndex(sources,i));
        if (![d[@kIOPSTypeKey] isEqual:@kIOPSInternalBatteryType] || ![d[@kIOPSIsPresentKey] boolValue]) continue;
        NSNumber *current = d[@kIOPSCurrentCapacityKey], *maximum = d[@kIOPSMaxCapacityKey];
        if (!current || maximum.doubleValue <= 0) continue;
        v[@"battery.percent"] = @(MIN(100, MAX(0, current.doubleValue / maximum.doubleValue * 100)));
        BOOL ac = [d[@kIOPSPowerSourceStateKey] isEqual:@kIOPSACPowerValue];
        BOOL charging = [d[@kIOPSIsChargingKey] boolValue];
        v[@"battery.ac_online"] = @(ac); v[@"battery.charging"] = @(charging);
        v[@"battery.discharging"] = @(!ac);
        NSNumber *empty = d[@kIOPSTimeToEmptyKey], *full = d[@kIOPSTimeToFullChargeKey];
        if (!ac && empty.doubleValue > 0) v[@"battery.estimated_time_to_empty"] = @(empty.doubleValue * 60);
        if (charging && full.doubleValue > 0) v[@"battery.time_to_full_seconds"] = @(full.doubleValue * 60);
        break; // Internal battery, not a UPS or Bluetooth accessory.
    }
    if (sources) CFRelease(sources); CFRelease(info);
}

static void disks(NSMutableDictionary *v) {
    // APFS's writable Data volume accounts for user data; the sealed root snapshot does not.
    const char *path = "/System/Volumes/Data"; struct statfs fs;
    if (statfs(path, &fs) != 0) { path = "/"; if (statfs(path, &fs) != 0) return; }
    double total = (double)fs.f_blocks * fs.f_bsize;
    if (total <= 0) return;
    double free = MIN(total, (double)fs.f_bfree * fs.f_bsize);
    v[@"disk.system.total_bytes"] = @(total);
    v[@"disk.system.free_bytes"] = @(free);
    v[@"disk.system.available_bytes"] = @((double)fs.f_bavail * fs.f_bsize);
    v[@"disk.system.used_bytes"] = @(total-free);
    v[@"disk.system.usage"] = @((total-free)/total*100);
    v[@"disk.system.root"] = [NSString stringWithUTF8String:path];
    v[@"disk.system.filesystem"] = [NSString stringWithUTF8String:fs.f_fstypename];
}

static void network(NSMutableDictionary *v) {
    int mib[] = {CTL_NET, PF_ROUTE, 0, 0, NET_RT_IFLIST2, 0}; size_t size = 0;
    if (sysctl(mib, 6, NULL, &size, NULL, 0) != 0 || size == 0) return;
    char *buffer = malloc(size); if (!buffer) return;
    if (sysctl(mib, 6, buffer, &size, NULL, 0) != 0) { free(buffer); return; }
    NSMutableDictionary *samples = [NSMutableDictionary new];
    NSMutableArray *names = [NSMutableArray new], *ipv4 = [NSMutableArray new], *ipv6 = [NSMutableArray new];
    uint64_t rx=0,tx=0,pr=0,ps=0,er=0,es=0;
    for (char *p=buffer; p+sizeof(struct if_msghdr)<=buffer+size;) {
        struct if_msghdr *header = (struct if_msghdr *)p;
        if (!header->ifm_msglen || p+header->ifm_msglen > buffer+size) break;
        if (header->ifm_type == RTM_IFINFO2 && header->ifm_msglen >= sizeof(struct if_msghdr2)) {
            struct if_msghdr2 *msg = (struct if_msghdr2 *)p; char name[IF_NAMESIZE];
            if (if_indextoname(msg->ifm_index, name) && strncmp(name,"en",2)==0 && (msg->ifm_flags & IFF_UP)) {
                NSString *key = [NSString stringWithUTF8String:name];
                samples[key] = @[@(msg->ifm_data.ifi_ibytes), @(msg->ifm_data.ifi_obytes)];
                [names addObject:key]; rx+=msg->ifm_data.ifi_ibytes; tx+=msg->ifm_data.ifi_obytes;
                pr+=msg->ifm_data.ifi_ipackets; ps+=msg->ifm_data.ifi_opackets;
                er+=msg->ifm_data.ifi_ierrors; es+=msg->ifm_data.ifi_oerrors;
            }
        }
        p+=header->ifm_msglen;
    }
    free(buffer);
    struct ifaddrs *list;
    if (getifaddrs(&list)==0) {
        for (struct ifaddrs *p=list;p;p=p->ifa_next) {
            if (!p->ifa_addr || !samples[[NSString stringWithUTF8String:p->ifa_name]]) continue;
            char address[INET6_ADDRSTRLEN]; int family=p->ifa_addr->sa_family;
            if (family==AF_INET && inet_ntop(AF_INET,&((struct sockaddr_in*)p->ifa_addr)->sin_addr,address,sizeof(address)))
                [ipv4 addObject:[NSString stringWithUTF8String:address]];
            if (family==AF_INET6 && inet_ntop(AF_INET6,&((struct sockaddr_in6*)p->ifa_addr)->sin6_addr,address,sizeof(address)))
                [ipv6 addObject:[NSString stringWithUTF8String:address]];
        }
        freeifaddrs(list);
    }
    v[@"__network_samples"]=samples;
    v[@"network.total_received_bytes"]=@(rx); v[@"network.total_sent_bytes"]=@(tx);
    v[@"network.total_transferred_bytes"]=@(rx+tx);
    v[@"network.packets_received"]=@(pr); v[@"network.packets_sent"]=@(ps);
    v[@"network.receive_errors"]=@(er); v[@"network.send_errors"]=@(es);
    v[@"network.active_interface_count"]=@(names.count);
    v[@"network.available"]=@(ipv4.count+ipv6.count>0);
    v[@"network.interface_names"]=[names componentsJoinedByString:@", "];
    v[@"network.ipv4_addresses"]=[ipv4 componentsJoinedByString:@", "];
    v[@"network.ipv6_addresses"]=[ipv6 componentsJoinedByString:@", "];
}

API char *ecp_snapshot(int mask) {
    @autoreleasepool {
        NSMutableDictionary *v=[NSMutableDictionary new];
        if (mask & 1) cpu(v); if (mask & 2) memory(v); if (mask & 4) battery(v);
        if (mask & 8) disks(v); if (mask & 16) network(v);
        return json(v);
    }
}
API char *ecp_gpus(void) {
    @autoreleasepool {
        NSMutableArray *devices=[NSMutableArray new];
        for (id<MTLDevice> device in MTLCopyAllDevices()) {
            [devices addObject:@{@"id":[NSString stringWithFormat:@"metal:%llu",device.registryID],
                @"name":device.name, @"unified":@(device.hasUnifiedMemory),
                @"budget":@(device.recommendedMaxWorkingSetSize), @"lowPower":@(device.lowPower), @"removable":@(device.removable)}];
        }
        return json(devices);
    }
}
// AppKit methods must only be called on Avalonia's UI thread.
API int ecp_hud_window(void *handle, int topmost) {
    @autoreleasepool {
        if (!NSThread.isMainThread || !handle) return 0;
        id object=(__bridge id)handle;
        NSWindow *window=[object isKindOfClass:NSWindow.class] ? object : ([object isKindOfClass:NSView.class] ? [object window] : nil);
        if (!window) return 0;
        window.ignoresMouseEvents=YES; window.hidesOnDeactivate=NO;
        window.opaque=NO; window.backgroundColor=NSColor.clearColor; window.hasShadow=NO;
        window.collectionBehavior=NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary | NSWindowCollectionBehaviorIgnoresCycle;
        window.level=topmost ? NSFloatingWindowLevel : NSNormalWindowLevel;
        return window.ignoresMouseEvents ? 1 : 0;
    }
}
API int ecp_hud_flags(void *handle) {
    @autoreleasepool {
        if (!NSThread.isMainThread || !handle) return 0;
        id object=(__bridge id)handle;
        NSWindow *window=[object isKindOfClass:NSWindow.class] ? object : ([object isKindOfClass:NSView.class] ? [object window] : nil);
        if (!window) return 0;
        return (window.ignoresMouseEvents ? 1 : 0) | ((window.collectionBehavior & NSWindowCollectionBehaviorCanJoinAllSpaces) ? 2 : 0)
            | (!window.opaque && window.backgroundColor.alphaComponent==0 ? 4 : 0);
    }
}
API int ecp_pointer(double *x, double *y) {
    CGEventRef event=CGEventCreate(NULL); if (!event) return 0;
    CGPoint p=CGEventGetLocation(event); CFRelease(event); *x=p.x; *y=p.y; return 1;
}
API char *ecp_display(void) {
    @autoreleasepool {
        if (!NSThread.isMainThread) return NULL;
        NSMutableDictionary *v=[NSMutableDictionary new];
        uint32_t count=0;
        if (CGGetActiveDisplayList(0,NULL,&count)!=kCGErrorSuccess || !count) return json(v);
        CGDirectDisplayID *ids=calloc(count,sizeof(CGDirectDisplayID));
        if (!ids) return NULL;
        CGRect desktop=CGRectNull;
        if (CGGetActiveDisplayList(count,ids,&count)==kCGErrorSuccess) {
            v[@"display.monitor_count"]=@(count);
            for (uint32_t i=0;i<count;i++) desktop=CGRectUnion(desktop,CGDisplayBounds(ids[i]));
            v[@"display.virtual_x_points"]=@(desktop.origin.x); v[@"display.virtual_y_points"]=@(desktop.origin.y);
            v[@"display.virtual_width_points"]=@(desktop.size.width); v[@"display.virtual_height_points"]=@(desktop.size.height);
        }
        free(ids);
        CGDirectDisplayID primary=CGMainDisplayID();
        CGDisplayModeRef mode=CGDisplayCopyDisplayMode(primary);
        if (mode) {
            v[@"display.primary_width_px"]=@(CGDisplayModeGetPixelWidth(mode));
            v[@"display.primary_height_px"]=@(CGDisplayModeGetPixelHeight(mode));
            CGDisplayModeRelease(mode);
        }
        for (NSScreen *screen in NSScreen.screens) {
            if ([screen.deviceDescription[@"NSScreenNumber"] unsignedIntValue]==primary) {
                v[@"display.scale_percent"]=@(100*screen.backingScaleFactor);
                v[@"display.system_dpi"]=@(96*screen.backingScaleFactor);
                break;
            }
        }
        return json(v);
    }
}
API char *ecp_clipboard(void) {
    @autoreleasepool {
        if (!NSThread.isMainThread) return NULL;
        NSPasteboard *board=NSPasteboard.generalPasteboard;
        NSString *text=[board stringForType:NSPasteboardTypeString] ?: @"";
        BOOL image=[board canReadObjectForClasses:@[NSImage.class] options:@{}];
        return json(@{@"text":text,@"hasImage":@(image),@"sequence":@(board.changeCount)});
    }
}
API int ecp_keychain_set(const char *account, const char *secret) {
    @autoreleasepool {
        NSDictionary *query=@{(__bridge id)kSecClass:(__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService:@"com.glacierglimmer.endfieldchargeplus.macos",
            (__bridge id)kSecAttrAccount:[NSString stringWithUTF8String:account]};
        NSData *data=[[NSString stringWithUTF8String:secret] dataUsingEncoding:NSUTF8StringEncoding];
        OSStatus status=SecItemUpdate((__bridge CFDictionaryRef)query,(__bridge CFDictionaryRef)@{(__bridge id)kSecValueData:data});
        if (status==errSecItemNotFound) {
            NSMutableDictionary *item=[query mutableCopy]; item[(__bridge id)kSecValueData]=data;
            status=SecItemAdd((__bridge CFDictionaryRef)item,NULL);
        }
        return (int)status;
    }
}
API char *ecp_keychain_get(const char *account) {
    @autoreleasepool {
        NSDictionary *query=@{(__bridge id)kSecClass:(__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService:@"com.glacierglimmer.endfieldchargeplus.macos",
            (__bridge id)kSecAttrAccount:[NSString stringWithUTF8String:account],
            (__bridge id)kSecReturnData:@YES, (__bridge id)kSecMatchLimit:(__bridge id)kSecMatchLimitOne};
        CFTypeRef result=NULL;
        if (SecItemCopyMatching((__bridge CFDictionaryRef)query,&result)!=errSecSuccess) return NULL;
        NSData *data=CFBridgingRelease(result);
        NSString *secret=[[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        return secret ? strdup(secret.UTF8String) : NULL;
    }
}
