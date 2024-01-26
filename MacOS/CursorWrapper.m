#import <Cocoa/Cocoa.h>

void SetCursorToArrow() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor arrow] set];
    });
}

void SetCursorToIBeam() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor iBeam] set];
    });
}

void SetCursorToCrosshair() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor crosshair] set];
    });
}

void SetCursorToOpenHand() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor openHand] set];
    });
}

void SetCursorToPointingHand() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor pointingHand] set];
    });
}

void SetCursorToResizeLeft() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeLeft] set];
    });
}

void SetCursorToResizeRight() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeRight] set];
    });
}

void SetCursorToResizeLeftRight() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeLeftRight] set];
    });
}

void SetCursorToResizeUp() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeUp] set];
    });
}

void SetCursorToResizeDown() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeDown] set];
    });
}

void SetCursorToResizeUpDown() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeUpDown] set];
    });
}

void SetCursorToDisappearingItem() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor disappearingItem] set];
    });
}

void SetCursorToIBeamCursorForVerticalLayout() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor IBeamCursorForVerticalLayout] set];
    });
}

void SetCursorToOperationNotAllowed() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor operationNotAllowed] set];
    });
}

void SetCursorToDragLink() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor dragLink] set];
    });
}

void SetCursorToDragCopy() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor dragCopy] set];
    });
}

void SetCursorToContextualMenu() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor contextualMenu] set];
    });
}