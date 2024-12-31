
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
        else if (strcmp(input, "install") == 0)
        {
           install();
        }
        else if (strcmp(input, "update") == 0)
        {
            printf("Updating...\n");
            update();
        }
        else if (strcmp(input, "hello") == 0)
        {
            printf("Hello!\n");
        }
        else if (strcmp(input, "help") == 0)
        {
            printf("Commands:\n");
            printf("exit - exit the program\n");
            printf("Install - install the program\n");
            printf("update - update the program\n");
            printf("hello - see a greeting\n");
            printf("help - see this message\n");
        }
        else
        {
            printf("Unknown command\n");
        }
    }
    
}