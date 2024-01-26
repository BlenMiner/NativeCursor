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

void SetCursorToResizeLeft() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeLeftCursor] set];
    });
}

void SetCursorToResizeRight() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeRightCursor] set];
    });
}

void SetCursorToResizeLeftRight() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeLeftRightCursor] set];
    });
}

void SetCursorToResizeUp() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeUpCursor] set];
    });
}

void SetCursorToResizeDown() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeDownCursor] set];
    });
}

void SetCursorToResizeUpDown() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor resizeUpDownCursor] set];
    });
}

void SetCursorToDisappearingItem() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor disappearingItemCursor] set];
    });
}

void SetCursorToOperationNotAllowed() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor operationNotAllowedCursor] set];
    });
}

void SetCursorToDragLink() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor dragLinkCursor] set];
    });
}

void SetCursorToDragCopy() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor dragCopyCursor] set];
    });
}

void SetCursorToContextualMenu() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor contextualMenuCursor] set];
    });
}

void SetCursorToIBeamCursorForVerticalLayout() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor IBeamCursorForVerticalLayout] set];
    });
}

void SetCursorToPointingHand() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSCursor pointingHandCursor] set];
    });
}