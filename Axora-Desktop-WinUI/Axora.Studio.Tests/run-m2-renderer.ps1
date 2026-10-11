[CmdletBinding()]
param(
    [string]$RuntimeSource = 'C:\Users\rajghosh\AppData\Local\Temp\AXORA-M2B1-TYPST-FEASIBILITY\runtime\typst.exe',
    [ValidateRange(5,300)][int]$TimeoutSeconds = 135,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:TEMP ('AXORA-M2B1-NATIVE-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$source = Join-Path $root 'renderer-probe.cpp'
@'
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <string>
#include <vector>
#include <userenv.h>
#include <cstdio>
#include <cstring>
#pragma comment(lib,"userenv.lib")
#pragma comment(lib,"advapi32.lib")
#pragma comment(lib,"ws2_32.lib")
int wmain(int argc,wchar_t** argv){
 if(argc<2)return 91; std::wstring mode=argv[1]; DWORD n=0;
 if(mode==L"profile-directories"){
  if(argc!=8)return 101;
  for(int i=2;i<argc;i++){
   std::wstring path=std::wstring(argv[i])+L"\\axora-profile-write-probe.txt";
   HANDLE f=CreateFileW(path.c_str(),GENERIC_WRITE,0,nullptr,CREATE_NEW,0,nullptr);DWORD error=GetLastError();
   if(f!=INVALID_HANDLE_VALUE){CloseHandle(f);return 102;}if(error!=ERROR_ACCESS_DENIED)return 103;
  }
  const char* text="PROFILE_ALL_SIX_DENIED_5";WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),text,24,&n,nullptr);return 0;
 }
 if(mode==L"profile-registry"){
  HKEY key=nullptr;HRESULT readable=GetAppContainerRegistryLocation(KEY_READ,&key);if(FAILED(readable))return 104;RegCloseKey(key);
  HRESULT writable=GetAppContainerRegistryLocation(KEY_READ|KEY_WRITE,&key);
  if(writable==HRESULT_FROM_WIN32(ERROR_ACCESS_DENIED)){const char* text="PROFILE_REGISTRY_DENIED_5";WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),text,25,&n,nullptr);return 0;}
  if(SUCCEEDED(writable)){const BYTE bytes[]="SYNTHETIC";LONG set=RegSetValueExW(key,L"AXORA_UNEXPECTED_WRITE",0,REG_BINARY,bytes,sizeof(bytes));if(set==0)RegDeleteValueW(key,L"AXORA_UNEXPECTED_WRITE");RegCloseKey(key);}return 105;
 }
 if(mode==L"profile-control"){
  if(argc!=3)return 109;
  const DWORD fileAccess[]={WRITE_DAC,WRITE_OWNER,DELETE,GENERIC_WRITE};for(DWORD access:fileAccess){
   HANDLE f=CreateFileW(argv[2],access,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,FILE_FLAG_BACKUP_SEMANTICS|FILE_FLAG_OPEN_REPARSE_POINT,nullptr);DWORD error=GetLastError();
   if(f!=INVALID_HANDLE_VALUE){CloseHandle(f);return 110;}if(error!=ERROR_ACCESS_DENIED)return 111;
  }
  const REGSAM keyAccess[]={WRITE_DAC,WRITE_OWNER,DELETE,KEY_WRITE};for(REGSAM access:keyAccess){
   HKEY key=nullptr;HRESULT hr=GetAppContainerRegistryLocation(access|KEY_READ,&key);if(SUCCEEDED(hr)){RegCloseKey(key);return 112;}if(hr!=HRESULT_FROM_WIN32(ERROR_ACCESS_DENIED))return 113;
  }
  const char* text="PROFILE_CONTROL_DENIED";WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),text,22,&n,nullptr);return 0;
 }
 if(mode==L"aap"){
  if(argc!=3)return 106;HANDLE f=CreateFileW(argv[2],GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr);if(f==INVALID_HANDLE_VALUE)return 107;
  char bytes[32]{};DWORD count=0;BOOL ok=ReadFile(f,bytes,sizeof(bytes),&count,nullptr);CloseHandle(f);if(!ok||count!=13||memcmp(bytes,"SYNTHETIC-AAP",13)!=0)return 108;
  const char* text="AAP_AMBIENT_READ_CONFIRMED";WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),text,26,&n,nullptr);return 0;
 }
 if(mode==L"hang"){Sleep(INFINITE);return 0;}
 if(mode==L"crash")return 19;
 if(mode==L"write"){
  if(argc!=3)return 98;std::wstring path=std::wstring(argv[2])+L"\\forbidden.txt";
  HANDLE f=CreateFileW(path.c_str(),GENERIC_WRITE,0,nullptr,CREATE_NEW,0,nullptr);DWORD e=GetLastError();
  if(f!=INVALID_HANDLE_VALUE){CloseHandle(f);return 99;}
  if(e==ERROR_ACCESS_DENIED){const char* text="WRITE_DENIED";WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),text,12,&n,nullptr);return 0;}return 100;
 }
 if(mode==L"flood"||mode==L"stderr"||mode==L"both"){
  std::vector<unsigned char> b(8192,0xff);
  for(int i=0;i<65536;i++){
   if(mode!=L"stderr")if(!WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),b.data(),(DWORD)b.size(),&n,nullptr))break;
   if(mode!=L"flood")if(!WriteFile(GetStdHandle(STD_ERROR_HANDLE),b.data(),(DWORD)b.size(),&n,nullptr))break;
  }return 0;
 }
 if(mode==L"tree"){
  wchar_t self[32768];GetModuleFileNameW(nullptr,self,32768);
  std::wstring cmd=L"\""+std::wstring(self)+L"\" hang";
  STARTUPINFOW si{};si.cb=sizeof(si);si.dwFlags=STARTF_USESTDHANDLES;
  si.hStdInput=GetStdHandle(STD_INPUT_HANDLE);si.hStdOutput=GetStdHandle(STD_OUTPUT_HANDLE);si.hStdError=GetStdHandle(STD_ERROR_HANDLE);
  PROCESS_INFORMATION pi{};if(!CreateProcessW(self,cmd.data(),nullptr,nullptr,TRUE,CREATE_NO_WINDOW,nullptr,nullptr,&si,&pi))return 92;
  CloseHandle(pi.hThread);CloseHandle(pi.hProcess);return 0;
 }
 if(mode==L"private"){
  if(argc!=3)return 93;
  HANDLE f=CreateFileW(argv[2],GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr);
  DWORD err=GetLastError();if(f!=INVALID_HANDLE_VALUE)CloseHandle(f);
  const char* text="PRIVATE_DENIED"; if(f==INVALID_HANDLE_VALUE&&err==ERROR_ACCESS_DENIED){WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),text,14,&n,nullptr);return 0;}return 94;
 }
 if(mode==L"network"){
  WSADATA d{};if(WSAStartup(MAKEWORD(2,2),&d))return 95;
  SOCKET s=socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);int err=s==INVALID_SOCKET?WSAGetLastError():0;
  if(s!=INVALID_SOCKET){u_long nb=1;ioctlsocket(s,FIONBIO,&nb);sockaddr_in a{};a.sin_family=AF_INET;a.sin_port=htons(443);inet_pton(AF_INET,"1.1.1.1",&a.sin_addr);
   int r=connect(s,(sockaddr*)&a,sizeof(a));err=r==0?0:WSAGetLastError();
   if(err==WSAEWOULDBLOCK){fd_set wr,er;FD_ZERO(&wr);FD_ZERO(&er);FD_SET(s,&wr);FD_SET(s,&er);timeval tv{3,0};
    int ready=select(0,nullptr,&wr,&er,&tv);if(ready>0){int size=sizeof(err);if(getsockopt(s,SOL_SOCKET,SO_ERROR,(char*)&err,&size))err=WSAGetLastError();}else err=ready==0?WSAETIMEDOUT:WSAGetLastError();}
   closesocket(s);}
  WSACleanup();if(err==WSAEACCES){const char* text="NETWORK_DENIED_10013";WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),text,20,&n,nullptr);return 0;}return 96;
 }
 unsigned char binary[]={'%','P','D','F','-',0,0xff,0x80,13,10};
 return WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),binary,sizeof(binary),&n,nullptr)&&n==sizeof(binary)?0:97;
}
'@ | Set-Content -LiteralPath $source -Encoding utf8
$vc = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231'
$kit = 'C:\Program Files (x86)\Windows Kits\10'
$helper = Join-Path $root 'renderer-probe.exe'
$compileLog = Join-Path $root 'probe-build.log'
& (Join-Path $vc 'bin\Hostx64\x64\cl.exe') /nologo /EHsc /MT /std:c++17 "/I$vc\include" "/I$kit\Include\10.0.26100.0\ucrt" "/I$kit\Include\10.0.26100.0\um" "/I$kit\Include\10.0.26100.0\shared" $source "/Fo$root\renderer-probe.obj" "/Fe$helper" /link "/LIBPATH:$vc\lib\x64" "/LIBPATH:$kit\Lib\10.0.26100.0\ucrt\x64" "/LIBPATH:$kit\Lib\10.0.26100.0\um\x64" *> $compileLog
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $compileLog; throw 'Native probe build failed' }
if ((Get-FileHash -LiteralPath $RuntimeSource).Hash -ne '081217A463ADB006F8894B44227FE4B9C9E91FC85F5463D5948E8370DB9BB31E') { throw 'External runtime hash mismatch' }
$env:AXORA_RENDERER_TEST_RUNTIME_SOURCE=$RuntimeSource
$env:AXORA_RENDERER_TEST_HELPER=$helper
$env:AXORA_RENDERER_TEST_ROOT=$root
Write-Output "Native evidence root: $root"
if ($PrepareOnly) { return }
$exe = Join-Path $PSScriptRoot 'bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\Axora.Studio.Tests.exe'
$out=Join-Path $root 'renderer.stdout.log'; $err=Join-Path $root 'renderer.stderr.log'
$child=Start-Process -FilePath $exe -ArgumentList '--group=STUDIO-M2-RENDERER' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
if (-not $child.WaitForExit($TimeoutSeconds*1000)) { Write-Output "BLOCKED: renderer process PID=$($child.Id), logs=$root"; exit 1 }
$child.WaitForExit()
$output=Get-Content -LiteralPath $out -Raw; Write-Output $output
Get-Content -LiteralPath $err
$lines=@($output -split '\r?\n' | Where-Object { $_.StartsWith('STUDIO-LEDGER ') })
if($child.ExitCode -ne 0 -or $lines.Count -ne 1){exit 1}
$l=$lines[0].Substring(14)|ConvertFrom-Json
if($l.group -ne 'STUDIO-M2-RENDERER' -or $l.totalRegisteredGroups -ne 7 -or $l.registeredGroups -ne 1 -or $l.executedGroups -ne 1 -or -not $l.complete -or $l.disposition -ne 'Pass' -or $l.failedAssertions -ne 0 -or $l.missing -ne 0 -or $l.duplicate -ne 0 -or $l.unknown -ne 0 -or $l.blocked -ne 0){exit 1}
exit 0

