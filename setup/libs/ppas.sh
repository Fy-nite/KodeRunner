#!/bin/sh
DoExitAsm ()
{ echo "An error occurred while assembling $1"; exit 1; }
DoExitLink ()
{ echo "An error occurred while linking $1"; exit 1; }
echo Linking ./libs/kodepas.o
OFS=$IFS
IFS="
"
/usr/bin/ld.bfd -b elf64-x86-64 -m elf_x86_64  -init FPC_SHARED_LIB_START -fini FPC_LIB_EXIT -soname kodepas.o  -shared  -L. -o ./libs/kodepas.o -T ./libs/link.res
if [ $? != 0 ]; then DoExitLink ./libs/kodepas.o; fi
IFS=$OFS
echo Linking ./libs/kodepas.o
OFS=$IFS
IFS="
"
/usr/bin/strip --discard-all --strip-debug ./libs/kodepas.o
if [ $? != 0 ]; then DoExitLink ./libs/kodepas.o; fi
IFS=$OFS
