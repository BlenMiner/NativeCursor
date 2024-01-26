#import <Cocoa/Cocoa.h>

void SetCursorToArrow() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor arrowCursor] set];
    });
}

void SetCursorToIBeam() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor IBeamCursor] set];
    });
}

void SetCursorToCrosshair() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor crosshairCursor] set];
    });
}

void SetCursorToResizeLeftRight() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeLeftRightCursor] set];
    });
}

void SetCursorToResizeUpDown() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeUpDownCursor] set];
    });
}

void SetCursorToOperationNotAllowed() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor operationNotAllowedCursor] set];
    });
}

void SetCursorToPointingHand() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor pointingHandCursor] set];
    });
}

void SetCursorToOpenHand() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor openHandCursor] set];
    });
}

void SetCursorToClosedHand() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor closedHandCursor] set];
    });
}

void SetCursorToBusy() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor busyButClickableCursor] set];
    });
}
