all: prepare
	# Compile Fortran code
	gfortran -fpic -c ./fort/kode.f90 -o ./libs/kode.o

	# Create a shared library from Fortran code
	gfortran -shared -o ./libs/libkode.so ./libs/kode.o

	# Compile Pascal code as shared library
	fpc -fPIC -o./libs/libkodepas.so ./pascal/kodepas.pas

	# Compile C code and link with Fortran and Pascal libraries
	gcc c/kode.c -L./libs -lkode -lkodepas -o ./build/kode

prepare:
	mkdir -p build libs

run: all
	clear
	mkdir -p build libs
	export LD_LIBRARY_PATH=./libs && ./build/kode
