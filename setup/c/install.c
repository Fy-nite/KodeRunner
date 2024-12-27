#include <stdlib.h>
#include <stdio.h>
#include <unistd.h>
void install() {
    printf("Installing...\n");
    char *cs_build = "dotnet build";
    
    // Clone the repository
    system("git clone https://git.gay/Finite/KodeRunner.git");
    // setup the directories 
    // the only one is the output directory
    system("mkdir output");
    // Change directory to the repository
    chdir("KodeRunner/runner");
    // so, we have 2 directories now. one is the actual thing, the other one is the default runnables.
    // we need to build the actual thing.

    // Build the actual thing
    int respose = system(cs_build);
    // suspecting that we did, we should check regardless
    if (respose != 0) {
        printf("Failed to build the program\n");
        // remove the output directory
        system("rm -rf ../../output");
        // remove the repository
        system("rm -rf ../KodeRunner");
        exit(1);
    }
    // if we did, we should move the built files to the output folder
    // and we are done for the first part
    
    // now we need to build the default runnables
    // we need to change the directory to the default runnables
    chdir("../KodeRunnerLibs/Runnables");
    // and build the default runnables
    respose = system(cs_build);
    // check if we did
    if (respose != 0) {
        printf("Failed to build the default runnables\n");
        // remove the output directory
        system("rm -rf ../../output");
        // remove the repository
        system("rm -rf ../../KodeRunner");
        exit(1);
    }
    // if we did, we should move the built files to the output folder
    system("mv ../../runner/bin/Debug/net8.0/* ../../output/");
    system("./../../output/KodeRunner --create_dirs");
    system("mv bin/Debug/net8.0/Runnables.dll ../../output/koderunner/Runnables");
    

    printf("Done!\n");
    printf("make sure you run the configurator when you run the program to create a configuration file\n");
    printf("We at finite don't want you to break the program so fast.\n");
    chdir("../../");
    // remove the repository
    system("rm -rf KodeRunner");
}