#import <Cocoa/Cocoa.h>

static void SetCursorOnMainThread(NSCursor *cursor) {
    if ([NSThread isMainThread]) {
        [cursor set];
        return;
    }

    dispatch_sync(dispatch_get_main_queue(), ^{
        [cursor set];
    });
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
