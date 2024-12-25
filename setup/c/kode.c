
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "headers/code.h"

int main() {
    int running = 1;
    printf("Welcome to the infomation center!\n");
    printf("Type 'hello' to see a greeting\n");
    printf("Type 'help' for more commands\n");
    while (running)
    {
        char input[100];
        printf("#: ");
        scanf("%s", input);
        if (strcmp(input, "exit") == 0)
        {
            running = 0;
        }
        else if (strcmp(input, "hello") == 0)
        {
            hello();
        }
        else if (strcmp(input, "fortran") == 0)
        {
            fortran_test();
        }
        else
        {
            printf("Unknown command\n");
        }
    }
    
}