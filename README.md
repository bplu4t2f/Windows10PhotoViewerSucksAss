# Windows10PhotoViewerSucksAss
An alternative to the default windows 10 photo viewer.

This program exists because the standard Windows 10 photo viewer sucks so much ass. It is fucking slow and annoying.

The program was hacked together in frustration and therefore the architecture is abysmal. But it's still better. Programming this was actually the very first thing I did after installing Windows 10 last year, and I've been using it since then.

## Controls:

| Button | Function |
|------|-----|
| LMB | Pan |
| LMB doubleclick | 1:1 |
| RMB | Zoom |
| W / D / MouseWheelDown | Next |
| A / S / MouseWheelUp | Previous |
| E | Open explorer at |
| F | Full file path to clipboard |
| C | File to clipboard |


![screenshot](img/screenshot.png)

## NatNumSort

The program now uses natural number sorting for the file list, similar to how Windows explorer sorts file names: ( a1.png, a2.png, a10.png ) instead of ( a1.png, a10,png, a2.png ).

It turned out that finding reasonable C# code that does this was tricky. Check out the NatnumSort class in this project, it doesn't allocate any heap memory, and it doesn't P/Invoke that one Windows function. As a bonus, it also preserves the (lexical) sorting order of hashes: (10a, 5ea) instead of (5ea, 10a). And it's just a static function.
