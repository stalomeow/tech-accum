from functools import wraps
from inspect import signature, Signature, Parameter
from typing import Literal
import ctypes

_DLL_TYPES = {
    '__cdecl': ctypes.CDLL,
    '__stdcall': ctypes.WinDLL
}

_dll_cache = {}

def _import_dll(name, calling_convention):
    try:
        return _dll_cache[name]
    except KeyError:
        dll = _DLL_TYPES[calling_convention](name)
        _dll_cache[name] = dll
        return dll

def _get_parameters_info(parameters):
    # (positional-only, positional or keyword, variadic positional)
    argtypes, defaults, argindex, i = [], [], {}, 0

    for param in parameters:
        if param.kind in (Parameter.KEYWORD_ONLY, Parameter.VAR_KEYWORD):
            raise SyntaxError(f'parameter {param.name} must not be a {param.kind.description} parameter')

        if param.kind != Parameter.VAR_POSITIONAL:
            if param.annotation == Parameter.empty:
                raise SyntaxError(f'parameter {param.name} must have an annotation')

            argtypes.append(param.annotation)
            argindex[param.name] = i
            i += 1

            if param.kind == Parameter.POSITIONAL_OR_KEYWORD and param.default != Parameter.empty:
                defaults.append(param.default)
    return argtypes, defaults, argindex, i

def extern_func(
    dll_name: str,
    entry_point: str = ...,
    calling_convention: Literal['__cdecl', '__stdcall'] = '__cdecl'
):
    def func_decorator(func):
        sig = signature(func)
        argtypes, defaults, argindex, argcount = _get_parameters_info(sig.parameters.values())
        callback = func() if argcount == 0 else func(*range(argcount))

        dll = _import_dll(dll_name, calling_convention)
        func_name = entry_point if entry_point != Ellipsis else func.__name__
        func_ptr = getattr(dll, func_name)
        func_ptr.argtypes = argtypes
        func_ptr.restype = None if sig.return_annotation == Signature.empty else sig.return_annotation

        print(f'load extern function: {func_name}', f'callback: {callback}', sep=', ')

        @wraps(func)
        def func_wrap(*args, **kwargs):
            if len(args) < argcount:
                args = list(args)
                args.extend(defaults)

            for k, v in kwargs.items():
                if k in argindex:
                    args[argindex[k]] = v

            value = func_ptr(*args)
            return callback(value) if callable(callback) else value
        return func_wrap
    return func_decorator