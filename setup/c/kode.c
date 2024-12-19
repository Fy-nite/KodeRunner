
#include <stdio.h>
#include <stdlib.h>
extern void fortran_test(void);
extern void hello(void);

int main() {
    int running = 1;
    while (running)
    {
        char input[100];
        printf("Enter a command: ");
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