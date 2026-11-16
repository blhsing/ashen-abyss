"""Copied into each venv's site-packages and loaded by ipv4only.pth.

On this network the IPv6 route to several CDNs (PyPI, PyTorch) crawls at ~30 kB/s while IPv4 is
normal. This makes Python's name resolution prefer IPv4 for this environment only; nothing
system-wide is changed. Delete ipv4only.pth and this file from site-packages to undo it.
"""
import socket

_orig_getaddrinfo = socket.getaddrinfo


def _getaddrinfo_ipv4_first(*args, **kwargs):
    res = _orig_getaddrinfo(*args, **kwargs)
    v4 = [r for r in res if r[0] == socket.AF_INET]
    return v4 or res


socket.getaddrinfo = _getaddrinfo_ipv4_first
