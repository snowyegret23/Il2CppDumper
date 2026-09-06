# -*- coding: utf-8 -*-
import json
import idaapi
import idc
import ida_funcs
import ida_kernwin
import ida_typeinf

processFields = [
	"ScriptMethod",
	"ScriptString",
	"ScriptMetadata",
	"ScriptMetadataMethod",
	"Addresses",
]

imageBase = idaapi.get_imagebase()

def get_addr(addr):
	return imageBase + addr

def set_name(addr, name):
	ret = idc.set_name(addr, name, idc.SN_NOWARN | idc.SN_NOCHECK)
	if ret == 0:
		new_name = name + '_' + str(addr)
		ret = idc.set_name(addr, new_name, idc.SN_NOWARN | idc.SN_NOCHECK)

def make_function(start, end):
	next_func = idc.get_next_func(start)
	if next_func < end:
		end = next_func
	if idc.get_func_attr(start, idc.FUNCATTR_START) == start:
		ida_funcs.del_func(start)
	ida_funcs.add_func(start, end)

def apply_signature(addr, signature):
	tif = ida_typeinf.tinfo_t()
	ida_typeinf.parse_decl(tif, None, signature, 0)
	if tif.empty() or not ida_typeinf.apply_tinfo(addr, tif, ida_typeinf.TINFO_DEFINITE):
		print("apply_tinfo failed:", hex(addr), signature)

def main():
	path = ida_kernwin.ask_file(False, '*.json', 'script.json from Il2cppdumper')
	if not path:
		return
	hpath = ida_kernwin.ask_file(False, '*.h', 'il2cpp.h from Il2cppdumper')
	if not hpath:
		return
	with open(path, 'r', encoding='utf-8-sig') as source:
		data = json.load(source)
	with open(hpath, 'r', encoding='utf-8-sig') as source:
		header = source.read()
	ida_kernwin.show_wait_box("Importing Il2CppDumper data...")
	try:
		if ida_typeinf.parse_decls(None, header, None, 0):
			print("Warning: Some header declarations could not be parsed.")
		if "Addresses" in data and "Addresses" in processFields:
			addresses = data["Addresses"]
			for index in range(len(addresses) - 1):
				if ida_kernwin.user_cancelled():
					raise KeyboardInterrupt
				start = get_addr(addresses[index])
				end = get_addr(addresses[index + 1])
				make_function(start, end)

		if "ScriptMethod" in data and "ScriptMethod" in processFields:
			scriptMethods = data["ScriptMethod"]
			for scriptMethod in scriptMethods:
				if ida_kernwin.user_cancelled():
					raise KeyboardInterrupt
				addr = get_addr(scriptMethod["Address"])
				name = scriptMethod["Name"]
				set_name(addr, name)
				signature = scriptMethod["Signature"]
				apply_signature(addr, signature)

		if "ScriptString" in data and "ScriptString" in processFields:
			index = 1
			scriptStrings = data["ScriptString"]
			for scriptString in scriptStrings:
				if ida_kernwin.user_cancelled():
					raise KeyboardInterrupt
				addr = get_addr(scriptString["Address"])
				value = scriptString["Value"]
				name = "StringLiteral_" + str(index)
				idc.set_name(addr, name, idc.SN_NOWARN)
				idc.set_cmt(addr, value, 1)
				index += 1

		if "ScriptMetadata" in data and "ScriptMetadata" in processFields:
			scriptMetadatas = data["ScriptMetadata"]
			for scriptMetadata in scriptMetadatas:
				if ida_kernwin.user_cancelled():
					raise KeyboardInterrupt
				addr = get_addr(scriptMetadata["Address"])
				name = scriptMetadata["Name"]
				set_name(addr, name)
				idc.set_cmt(addr, name, 1)
				if scriptMetadata["Signature"] is not None:
					signature = scriptMetadata["Signature"]
					apply_signature(addr, signature)

		if "ScriptMetadataMethod" in data and "ScriptMetadataMethod" in processFields:
			scriptMetadataMethods = data["ScriptMetadataMethod"]
			for scriptMetadataMethod in scriptMetadataMethods:
				if ida_kernwin.user_cancelled():
					raise KeyboardInterrupt
				addr = get_addr(scriptMetadataMethod["Address"])
				name = scriptMetadataMethod["Name"]
				methodAddr = get_addr(scriptMetadataMethod["MethodAddress"])
				set_name(addr, name)
				idc.set_cmt(addr, name, 1)
				idc.set_cmt(addr, '{0:X}'.format(methodAddr), 0)

		print('Script finished!')
	except KeyboardInterrupt:
		print('Script cancelled; completed annotations have been kept.')
	finally:
		ida_kernwin.hide_wait_box()

main()

