FOR /d /r src\Content %%d IN (bin) DO @IF EXIST %%d rd /s /q %%d
FOR /d /r src\Content %%d IN (obj) DO @IF EXIST %%d rd /s /q %%d
FOR /d /r src\Content %%d IN (logs) DO @IF EXIST %%d rd /s /q %%d
FOR /d /r src\Content %%d IN (.idea) DO @IF EXIST %%d rd /s /q %%d
FOR /d /r src\Content %%d IN (.vs) DO @IF EXIST %%d rd /s /q %%d
FOR /r src\Content %%d IN (*.user) DO del %%d
