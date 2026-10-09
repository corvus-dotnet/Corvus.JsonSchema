{ Compiler settings shared by every unit of the Corvus JSON Schema evaluator.

  The source is written in the Delphi dialect, so that Delphi can compile it as it is. Free Pascal (3.2.2 or later)
  is the compiler it is built and tested with. }

{$IFDEF FPC}
  {$MODE DELPHI}
  {$CODEPAGE UTF8}
  {$MODESWITCH ADVANCEDRECORDS}
  {$INLINE ON}
  {$MACRO OFF}
  { Calling a function without using its result is ordinary in this code. }
  {$WARN 5024 OFF}
{$ELSE}
  {$POINTERMATH ON}
{$ENDIF}

{ Long strings (reference counted), never ShortString. }
{$H+}
{ Typed @ operator, so that a pointer of the wrong type is a compile error. }
{$T+}
{ Every index into an array is checked: an index out of range raises ERangeError and never reads other memory. }
{$R+}
{ Integer overflow is not checked: hashes and the number parser wrap on purpose, and say so where they do. }
{$Q-}
{ No assertions in a release build unless asked for. }
{$IFNDEF CORVUS_ASSERTIONS}{$C-}{$ENDIF}
