@echo off
setlocal enabledelayedexpansion
for /L %%G in (1,1,723) do ( & ::  replace 723 with the current highest ID in the 'npc' table
    if not exist %%G.png (
        copy default.png generated\%%G.png
    )
)
