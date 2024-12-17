all:
	gcc -shared -fPIC -o libkode.so kode.c
	gfortran -o kode kode.f90 -L. -lkode

run: all
	clear
	export LD_LIBRARY_PATH=. && ./kode