// File_name: main.c
// Project: meow

// meow
// C project
#define chair char
#include <stdio.h>
#include <malloc.h>
int main() {
    printf("Hello from meow!\n");
	chair* c = (char*)malloc(1023);
 	scanf("%s",c);
	printf("%s",c);
	free(c);
    return 0;
}
