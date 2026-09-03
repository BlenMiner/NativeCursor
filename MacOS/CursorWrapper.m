#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>

// Native cursor override for the Unity macOS player.
//
// Unity and AppKit both call -[NSCursor set] on their own schedule (mouse moves, cursor rects,
// focus changes), which would replace the cursor requested through NativeCursor. The wrapper
// swizzles -[NSCursor set] and, while an override is active and the pointer is inside the content
// area of one of this application's windows, redirects those calls to the active cursor.
//
// Calls made while the pointer is over the title bar, window edges, menus, or another application
// pass through unchanged so AppKit keeps its own cursors there.
//
// Compiled without ARC (see .github/workflows/build-native-cursor-plugin.yml); retain/release are manual.

typedef void (*CursorSetImplementation)(id self, SEL _cmd);

static NSCursor *activeCursor = nil;
static BOOL cursorOverrideEnabled = NO;
static BOOL applyingNativeCursor = NO;
static CursorSetImplementation originalCursorSet = NULL;

static void ApplyCursor(NSCursor *cursor);

// YES when the pointer is over the content view of a visible window that belongs to this process.
static BOOL PointerInsideOwnContentArea(void) {
    NSPoint screenPoint = [NSEvent mouseLocation];
    NSInteger windowNumber = [NSWindow windowNumberAtPoint:screenPoint belowWindowWithWindowNumber:0];

    if (windowNumber <= 0) {
        return NO;
    }

    // Returns nil for windows owned by other applications.
    NSWindow *window = [NSApp windowWithWindowNumber:windowNumber];

    if (window == nil || ![window isVisible]) {
        return NO;
    }

    NSView *contentView = [window contentView];

    if (contentView == nil) {
        return NO;
    }

    NSRect screenRect = NSMakeRect(screenPoint.x, screenPoint.y, 0, 0);
    NSPoint windowPoint = [window convertRectFromScreen:screenRect].origin;
    NSPoint viewPoint = [contentView convertPoint:windowPoint fromView:nil];

    return [contentView mouse:viewPoint inRect:[contentView bounds]];
}

static void NativeCursorSet(id self, SEL _cmd) {
    if (cursorOverrideEnabled && !applyingNativeCursor && activeCursor != nil && self != activeCursor &&
        PointerInsideOwnContentArea()) {
        ApplyCursor(activeCursor);
        return;
    }

    if (originalCursorSet != NULL) {
        originalCursorSet(self, _cmd);
    }
}

static void InstallCursorOverride(void) {
    if (originalCursorSet != NULL) {
        return;
    }

    Method setMethod = class_getInstanceMethod([NSCursor class], @selector(set));

    if (setMethod == NULL) {
        return;
    }

    originalCursorSet = (CursorSetImplementation)method_setImplementation(setMethod, (IMP)NativeCursorSet);
}

// Applies the cursor immediately, bypassing the redirect. Callers decide whether the pointer position
// makes that appropriate.
static void ApplyCursor(NSCursor *cursor) {
    if (cursor == nil) {
        return;
    }

    InstallCursorOverride();
    applyingNativeCursor = YES;

    if (originalCursorSet != NULL) {
        originalCursorSet(cursor, @selector(set));
    } else {
        [cursor set];
    }

    applyingNativeCursor = NO;
}

// Applies the active cursor only when the pointer is inside our content area. Elsewhere the next
// AppKit or Unity -set call is redirected once the pointer re-enters, so nothing needs to happen now.
static void ApplyActiveCursorIfInside(void) {
    if (activeCursor != nil && PointerInsideOwnContentArea()) {
        ApplyCursor(activeCursor);
    }
}

static void SetActiveCursor(NSCursor *cursor) {
    if (cursor == nil) {
        return;
    }

    InstallCursorOverride();

    [cursor retain];
    [activeCursor release];
    activeCursor = cursor;
    cursorOverrideEnabled = YES;
    ApplyActiveCursorIfInside();
}

static void RunOnMainThread(void (^block)(void)) {
    if ([NSThread isMainThread]) {
        block();
        return;
    }

    dispatch_sync(dispatch_get_main_queue(), block);
}

static void SetCursorOnMainThread(NSCursor *cursor) {
    RunOnMainThread(^{
        SetActiveCursor(cursor);
    });
}

static void DisableCursorOverride(void) {
    cursorOverrideEnabled = NO;
    [activeCursor release];
    activeCursor = nil;
}

// -[NSCursor busyButClickableCursor] is private API. It is looked up at runtime and falls back to the
// arrow when absent. Do not rely on it for Mac App Store submissions.
static NSCursor *BusyCursor(void) {
    SEL selector = NSSelectorFromString(@"busyButClickableCursor");

    if (![NSCursor respondsToSelector:selector]) {
        return [NSCursor arrowCursor];
    }

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Warc-performSelector-leaks"
    return [NSCursor performSelector:selector];
#pragma clang diagnostic pop
}

// macOS 15 exposes real corner-resize cursors. Older systems (or older SDKs) fall back to the
// single-direction cursors that were used before.
#if defined(__MAC_15_0) && __MAC_OS_X_VERSION_MAX_ALLOWED >= __MAC_15_0
#define NATIVE_CURSOR_HAS_FRAME_RESIZE 1
#else
#define NATIVE_CURSOR_HAS_FRAME_RESIZE 0
#endif

static NSCursor *DiagonalNorthWestSouthEastCursor(void) {
#if NATIVE_CURSOR_HAS_FRAME_RESIZE
    if (@available(macOS 15.0, *)) {
        return [NSCursor frameResizeCursorFromPosition:NSCursorFrameResizePositionTopLeft
                                          inDirections:NSCursorFrameResizeDirectionsAll];
    }
#endif
    return [NSCursor resizeUpCursor];
}

static NSCursor *DiagonalNorthEastSouthWestCursor(void) {
#if NATIVE_CURSOR_HAS_FRAME_RESIZE
    if (@available(macOS 15.0, *)) {
        return [NSCursor frameResizeCursorFromPosition:NSCursorFrameResizePositionTopRight
                                          inDirections:NSCursorFrameResizeDirectionsAll];
    }
#endif
    return [NSCursor resizeDownCursor];
}

void SetCursorToArrow(void) {
    SetCursorOnMainThread([NSCursor arrowCursor]);
}

void SetCursorToIBeam(void) {
    SetCursorOnMainThread([NSCursor IBeamCursor]);
}

void SetCursorToCrosshair(void) {
    SetCursorOnMainThread([NSCursor crosshairCursor]);
}

void SetCursorToResizeLeftRight(void) {
    SetCursorOnMainThread([NSCursor resizeLeftRightCursor]);
}

void SetCursorToResizeUpDown(void) {
    SetCursorOnMainThread([NSCursor resizeUpDownCursor]);
}

// ResizeDiagonalLeft: the northwest/southeast shape, matching Windows IDC_SIZENWSE and CSS nwse-resize.
void SetCursorToResizeUp(void) {
    SetCursorOnMainThread(DiagonalNorthWestSouthEastCursor());
}

// ResizeDiagonalRight: the northeast/southwest shape, matching Windows IDC_SIZENESW and CSS nesw-resize.
void SetCursorToResizeDown(void) {
    SetCursorOnMainThread(DiagonalNorthEastSouthWestCursor());
}

void SetCursorToOperationNotAllowed(void) {
    SetCursorOnMainThread([NSCursor operationNotAllowedCursor]);
}

void SetCursorToPointingHand(void) {
    SetCursorOnMainThread([NSCursor pointingHandCursor]);
}

void SetCursorToOpenHand(void) {
    SetCursorOnMainThread([NSCursor openHandCursor]);
}

void SetCursorToClosedHand(void) {
    SetCursorOnMainThread([NSCursor closedHandCursor]);
}

void SetCursorToBusy(void) {
    SetCursorOnMainThread(BusyCursor());
}

void ReapplyNativeCursor(void) {
    RunOnMainThread(^{
        if (cursorOverrideEnabled) {
            ApplyActiveCursorIfInside();
        }
    });
}

void DisableNativeCursorOverride(void) {
    RunOnMainThread(^{
        DisableCursorOverride();
    });
}
