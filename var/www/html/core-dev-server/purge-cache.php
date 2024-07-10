<?php

if (@$_GET['key'] !== 'SECRET') {
	echo '<p>Enter access key:</p><form method="get"><input type="text" name="key" /><input type="submit" /></form>';
	die();
}

$queue = array('./vow-src-upload/src');

$counter = 0;

while (!empty($queue)) {
	$dir = array_shift($queue);
	foreach (glob($dir.'/*') as $entry) {
		if (is_dir($entry)) {
			# echo "$entry is a directory, adding to queue<br>";
			$queue[] = $entry;
		} else if (pathinfo($entry, PATHINFO_EXTENSION) === 'dat') {
			# echo "$entry is a binary index file, deleting<br>";
			unlink($entry);
			$counter++;
		}/* else {
			echo "$entry is to be kept<br>";
		}*/
	}
}

echo "$counter files deleted.<br>";
echo '<a href="https://voicesofwynn.com/files/core-dev-server/generate-cache.php">Regenerate cache</a> (recommended before releasing to production)';
