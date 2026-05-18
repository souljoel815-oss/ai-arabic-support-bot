{{-- T096 — shared email layout. Used by every Mailable subclass via
     `@extends('emails._layout')`. Keeps the brand bar + footer in one
     place so a brand tweak doesn't sprawl across 6 templates.

     Inline styles only — mail clients (Gmail / Outlook / Apple Mail)
     ignore <style> blocks in <head> and need everything on-element. --}}
<!DOCTYPE html>
<html lang="{{ $lang }}" dir="{{ $dir }}">
<head>
    <meta charset="UTF-8">
    <title>@yield('title', 'DaftarX')</title>
</head>
<body style="margin:0; padding:0; background:#f4efe8; font-family:'Cairo','Helvetica Neue',Arial,sans-serif; direction:{{ $dir }};">
    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#f4efe8; padding:32px 16px;">
        <tr>
            <td align="center">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="max-width:600px; background:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 1px 4px rgba(0,0,0,0.04);">

                    {{-- Brand bar --}}
                    <tr>
                        <td style="padding:24px 32px; border-bottom:3px solid #c89933;">
                            <span style="font-size:22px; font-weight:bold; color:#c89933; letter-spacing:-0.5px;">DaftarX</span>
                            <span style="font-size:12px; color:#a39d96; margin-{{ $isArabic ? 'right' : 'left' }}:8px;">دفترx</span>
                        </td>
                    </tr>

                    {{-- Body --}}
                    <tr>
                        <td style="padding:32px; color:#1a1816; font-size:15px; line-height:1.7;">
                            @yield('body')
                        </td>
                    </tr>

                    {{-- Footer --}}
                    <tr>
                        <td style="padding:20px 32px; background:#faf8f5; border-top:1px solid #e8e3dd; color:#6e6862; font-size:12px; line-height:1.6;">
                            @if ($isArabic)
                                وصلتك هذه الرسالة من <strong>DaftarX</strong> — منصة الضرائب والمحاسبة المصرية.
                                <br>إن لم تكن تنتظر هذه الرسالة، تجاهلها بأمان.
                            @else
                                You received this from <strong>DaftarX</strong> — Egypt Tax &amp; Accounting Suite.
                                <br>If you weren't expecting this, you can safely ignore it.
                            @endif
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>
