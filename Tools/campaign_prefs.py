#!/usr/bin/env python3
"""Back up / restore only FinalDefense.Campaign.v1 in one macOS preference domain.

Run backup before manually starting the Mac player, restore after it exits.
No other preference keys are read, written, deleted or printed.
"""
import argparse
import ctypes as c
import json
import os
from pathlib import Path

KEY = 'FinalDefense.Campaign.v1'
cf = c.CDLL('/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation')
P = c.c_void_p
cf.CFStringCreateWithCString.argtypes = [P, c.c_char_p, c.c_uint32]; cf.CFStringCreateWithCString.restype = P
cf.CFPreferencesCopyAppValue.argtypes = [P, P]; cf.CFPreferencesCopyAppValue.restype = P
cf.CFPreferencesSetAppValue.argtypes = [P, P, P]
cf.CFPreferencesAppSynchronize.argtypes = [P]; cf.CFPreferencesAppSynchronize.restype = c.c_bool
cf.CFStringGetLength.argtypes = [P]; cf.CFStringGetLength.restype = c.c_long
cf.CFStringGetCString.argtypes = [P, c.c_char_p, c.c_long, c.c_uint32]; cf.CFStringGetCString.restype = c.c_bool
cf.CFGetTypeID.argtypes = [P]; cf.CFGetTypeID.restype = c.c_ulong
cf.CFStringGetTypeID.restype = c.c_ulong
cf.CFRelease.argtypes = [P]
UTF8 = 0x08000100

def string(value): return cf.CFStringCreateWithCString(None, value.encode('utf-8'), UTF8)
def read_value(key, domain):
    ref = cf.CFPreferencesCopyAppValue(key, domain)
    if not ref: return False, ''
    try:
        if cf.CFGetTypeID(ref) != cf.CFStringGetTypeID():
            raise RuntimeError('Campaign key is not a string; refusing to alter it.')
        buf = c.create_string_buffer(cf.CFStringGetLength(ref) * 4 + 1)
        if not cf.CFStringGetCString(ref, buf, len(buf), UTF8): raise RuntimeError('Cannot decode campaign value')
        return True, buf.value.decode('utf-8')
    finally: cf.CFRelease(ref)

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('action', choices=['backup', 'restore', 'status'])
    ap.add_argument('--domain', required=True, help='Verified Mac player: com.DefaultCompany.2D-URP; Unity editor tests: unity.DefaultCompany.game')
    ap.add_argument('--file', type=Path, help='Private backup JSON path; never commit this file')
    ap.add_argument('--clear', action='store_true', help='After backup, clear only the campaign key for a fresh manual run')
    args = ap.parse_args()
    key, domain = string(KEY), string(args.domain)
    try:
        cf.CFPreferencesAppSynchronize(domain)
        if args.action == 'status':
            existed, _ = read_value(key, domain)
            print(json.dumps({'domain': args.domain, 'key': KEY, 'exists': existed})); return
        if args.file is None: ap.error('--file is required for backup/restore')
        if args.action == 'backup':
            existed, value = read_value(key, domain)
            args.file.parent.mkdir(parents=True, exist_ok=True)
            fd = os.open(args.file, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            with os.fdopen(fd, 'w') as out:
                json.dump({'domain': args.domain, 'key': KEY, 'existed': existed, 'value': value}, out)
            if args.clear:
                cf.CFPreferencesSetAppValue(key, None, domain)
                if not cf.CFPreferencesAppSynchronize(domain): raise RuntimeError('Cannot synchronize preference update')
            print('Backed up exactly one campaign key: ' + str(args.file))
        else:
            data = json.loads(args.file.read_text())
            if data.get('domain') != args.domain or data.get('key') != KEY:
                raise RuntimeError('Backup domain/key mismatch; refusing restore')
            value_ref = string(data['value']) if data['existed'] else None
            try:
                cf.CFPreferencesSetAppValue(key, value_ref, domain)
                if not cf.CFPreferencesAppSynchronize(domain): raise RuntimeError('Cannot synchronize restored preference')
            finally:
                if value_ref: cf.CFRelease(value_ref)
            if read_value(key, domain) != (data['existed'], data['value']): raise RuntimeError('Restored key did not verify')
            args.file.unlink()
            print('Restored original campaign key and removed private backup.')
    finally: cf.CFRelease(key); cf.CFRelease(domain)
if __name__ == '__main__': main()
