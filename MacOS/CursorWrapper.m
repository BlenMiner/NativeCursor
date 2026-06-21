#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>

typedef void (*CursorSetImplementation)(id self, SEL _cmd);

static NSCursor *activeCursor = nil;
static BOOL cursorOverrideEnabled = NO;
static BOOL applyingNativeCursor = NO;
static CursorSetImplementation originalCursorSet = NULL;

static void ApplyCursor(NSCursor *cursor);

static void NativeCursorSet(id self, SEL _cmd) {
    if (cursorOverrideEnabled && !applyingNativeCursor && activeCursor != nil && self != activeCursor) {
        ApplyCursor(activeCursor);
        return;
    }

    if (originalCursorSet != NULL) {
        originalCursorSet(self, _cmd);
    }
}

static void InstallCursorOverride() {
    if (originalCursorSet != NULL) {
        return;
    }

    Method setMethod = class_getInstanceMethod([NSCursor class], @selector(set));
    if (setMethod == NULL) {
        return;
    }

    originalCursorSet = (CursorSetImplementation)method_setImplementation(setMethod, (IMP)NativeCursorSet);
}

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

static void SetActiveCursor(NSCursor *cursor) {
    if (cursor == nil) {
        return;
    }

    InstallCursorOverride();

    [cursor retain];
    [activeCursor release];
    activeCursor = cursor;
    cursorOverrideEnabled = YES;
    ApplyCursor(activeCursor);
}

static void SetCursorOnMainThread(NSCursor *cursor) {
    if ([NSThread isMainThread]) {
        SetActiveCursor(cursor);
        return;
    }

    dispatch_sync(dispatch_get_main_queue(), ^{
        SetActiveCursor(cursor);
    });
}

static void DisableCursorOverride() {
    cursorOverrideEnabled = NO;
    [activeCursor release];
    activeCursor = nil;
}

static NSCursor *BusyCursor() {
    SEL selector = NSSelectorFromString(@"busyButClickableCursor");

    if (![NSCursor respondsToSelector:selector]) {
        return [NSCursor arrowCursor];
    }

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Warc-performSelector-leaks"
    return [NSCursor performSelector:selector];
#pragma clang diagnostic pop
}

void SetCursorToArrow() {
    SetCursorOnMainThread([NSCursor arrowCursor]);
}

void SetCursorToIBeam() {
    SetCursorOnMainThread([NSCursor IBeamCursor]);
}

void SetCursorToCrosshair() {
    SetCursorOnMainThread([NSCursor crosshairCursor]);
}

void SetCursorToResizeLeftRight() {
    SetCursorOnMainThread([NSCursor resizeLeftRightCursor]);
}

void SetCursorToResizeUpDown() {
    SetCursorOnMainThread([NSCursor resizeUpDownCursor]);
}

void SetCursorToResizeUp() {
    SetCursorOnMainThread([NSCursor resizeUpCursor]);
}

void SetCursorToResizeDown() {
    SetCursorOnMainThread([NSCursor resizeDownCursor]);
}

void SetCursorToOperationNotAllowed() {
    SetCursorOnMainThread([NSCursor operationNotAllowedCursor]);
}

void SetCursorToPointingHand() {
    SetCursorOnMainThread([NSCursor pointingHandCursor]);
}

void SetCursorToOpenHand() {
    SetCursorOnMainThread([NSCursor openHandCursor]);
}

void SetCursorToClosedHand() {
    SetCursorOnMainThread([NSCursor closedHandCursor]);
}

void SetCursorToBusy() {
    SetCursorOnMainThread(BusyCursor());
}

void ReapplyNativeCursor() {
    if ([NSThread isMainThread]) {
        ApplyCursor(activeCursor);
        return;
    }

    dispatch_sync(dispatch_get_main_queue(), ^{
        ApplyCursor(activeCursor);
    });
}

void DisableNativeCursorOverride() {
    if ([NSThread isMainThread]) {
        DisableCursorOverride();
        return;
    }

    dispatch_sync(dispatch_get_main_queue(), ^{
        DisableCursorOverride();
    });
}
