all: prepare
	gcc -o build/kode c/kode.c 
prepare:
	mkdir -p build libs

run: all
	clear
	mkdir -p build libs
	./build/kode
