library kodepas;

uses
  SysUtils;
 
{$mode objfpc}

function hello(): integer; cdecl;

begin
    writeln('hello from pascal');
end;

exports
  hello;
end.
