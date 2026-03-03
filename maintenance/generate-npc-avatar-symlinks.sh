#!/bin/bash

DEFAULT_IMAGE_CHECKSUM="a1b5602ac310dc954fdb06a1a81b21ba9d4a6634"

echo Enter the max NPC ID to check and generate a symlink for:
read max

for ((i = 1; i <= max; i++));
do
  FILE="../dynamic/npcs/$i.png"
  if [ -h $FILE ]; then
      echo "Avatar for NPC ID $i is already a symlink."
  elif [ -f $FILE ]; then
        HASH=$(sha1sum $FILE | awk '{print $1;}')
        if [[ "$HASH" = "$DEFAULT_IMAGE_CHECKSUM" ]]; then
          echo "Avatar for NPC ID $i exists and it is the default one. Replacing with symlink."
          ln -sf default.png $FILE
        else
          echo "Avatar for NPC ID $i exists."
        fi
  else
    echo "Avatar for NPC ID $i does not exist. Creating symlink."
    ln -s ../dynamic/npcs/default.png $FILE
  fi
done

echo "Finished."
