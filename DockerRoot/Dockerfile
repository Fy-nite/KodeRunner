FROM docker.io/forumi0721/alpine-dotnet-runtime
COPY ../KodeRunner-linux64.tar.gz /tmp/
RUN  mkdir /KodeRunner && cd /KodeRunner && tar xvzf /tmp/KodeRunner-linux64.tar.gz
RUN /KodeRunner/KodeRunner --init 

