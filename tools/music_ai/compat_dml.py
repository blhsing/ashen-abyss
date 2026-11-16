"""Test-only shim for the DirectML environment (torch 2.4.1 + torch-directml 0.2.5.dev240914).

diffusers 0.39 (the first release with AceStepPipeline) expects torch >= 2.5:
  * it registers optional flash-attention wrappers with `torch.library.custom_op`; torch 2.4.1 cannot parse their
    (string) annotations: "infer_schema(func): Parameter q has unsupported type torch.Tensor"
  * it imports `torch.nn.attention.flex_attention`, which does not exist before torch 2.5
Neither feature is used by the ACE-Step pipeline, so on old torch they are replaced by inert stand-ins before
diffusers is imported.  This is only good enough to find out whether DirectML can run the model at all.
"""
import sys
import types

import torch


def apply():
    major, minor = (int(v) for v in torch.__version__.split("+")[0].split(".")[:2])
    if (major, minor) >= (2, 5):
        return False
    torch.library.custom_op = lambda *a, **k: (lambda f: f)
    torch.library.register_fake = lambda *a, **k: (lambda f: f)
    name = "torch.nn.attention.flex_attention"
    if name not in sys.modules:
        stub = types.ModuleType(name)

        def _unavailable(*a, **k):
            raise RuntimeError("flex_attention is not available in torch %s" % torch.__version__)

        stub.flex_attention = _unavailable
        stub.create_block_mask = _unavailable
        stub.BlockMask = type("BlockMask", (), {})
        sys.modules[name] = stub
        import torch.nn.attention as _att
        _att.flex_attention = stub
    return True
