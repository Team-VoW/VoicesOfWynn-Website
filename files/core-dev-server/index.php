<?php

// Values of constants for example request: http(s)://example.com/files/open-dir/releases/linux

define('REQUESTED_RELATIVE_PATH', urldecode(rtrim(parse_url($_SERVER['REQUEST_URI'])['path'], '/')));
# /files/open-dir/releases/linux (for example)

define('OPEN_DIRECTORY_ABSOLUTE_PATH', __DIR__);
# /home/html/voicesofwynn.com/public_html/files/open-dir

define('ROOT_ABSOLUTE_PATH', rtrim($_SERVER['DOCUMENT_ROOT'], '/'));
# /home/html/voicesofwynn.com/public_html

define('OPEN_DIRECTORY_RELATIVE_PATH', substr(OPEN_DIRECTORY_ABSOLUTE_PATH, strlen(ROOT_ABSOLUTE_PATH)));
# /files/open-dir

define('REQUESTED_PATH_FROM_OPEN_DIRECTORY_ROOT', substr(REQUESTED_RELATIVE_PATH, strlen(OPEN_DIRECTORY_RELATIVE_PATH)));
# /releases/linux

define('REQUESTED_ABSOLUTE_PATH', OPEN_DIRECTORY_ABSOLUTE_PATH.REQUESTED_PATH_FROM_OPEN_DIRECTORY_ROOT);
# /home/html/voicesofwynn.com/public_html/files/open-dir/releases/linux

if (!is_dir(REQUESTED_ABSOLUTE_PATH)) {
    header('HTTP/1.0 404 Not Found');
    exit();
}

if (isset($_GET['list']) || substr(REQUESTED_PATH_FROM_OPEN_DIRECTORY_ROOT, 0, 19) !== '/vow-src-upload/src') {
	//Generate index binaries only for the /vow-src-upload/src directory
    showFiles(REQUESTED_ABSOLUTE_PATH);
} else {
    if (!file_exists(REQUESTED_ABSOLUTE_PATH.'/index.dat')) {
        //Index binary doesn't exist yet, generate it
        generateBinaryIndexFile(REQUESTED_ABSOLUTE_PATH);
    }
    downloadIndexBinary(REQUESTED_ABSOLUTE_PATH);
}

function showFiles($path) {
	echo '<html><head><title>Open Directory</title></head><body>';
	
	if (file_exists($path.'/_DIR-INFO-TOP.html')) {
        include $path.'/_DIR-INFO-TOP.html';
    }
	
    $entries = array_diff(scandir($path), array('.', '..'));
    $filesHtml = '';

    if ($path !== OPEN_DIRECTORY_ABSOLUTE_PATH) {
        echo '<a href="..?list" style="text-decoration: none;">📁 </a><a href="..?list"><i>Parent directory</i></a><br>';
    } else {
        echo "<span>⚠ This is the root of this open directory, going to the parent directory will get you an error.</span><br>";
    }

    foreach ($entries as $entry)
    {
        if (is_dir($path.'/'.$entry)) {
            echo '<a href="'.$entry.'?list" style="text-decoration: none;">📁 </a><a href="'.$entry.'?list">'.$entry.'</a><br>';
        }
        else {
            if (in_array($entry, array('.htaccess', 'index.php', 'index.html', '_DIR-INFO-TOP.html', '_DIR-INFO-BOTTOM.html'))) {
                $filesHtml .= '<span>📜 '.$entry.'</span><br>';
            } else {
                $filesHtml .= '<a href="'.$entry.'" style="text-decoration: none;">📄 </a><a href="'.$entry.'">'.$entry.'</a><br>';
            }
        }
    }

    echo $filesHtml;
    
    if (file_exists($path.'/_DIR-INFO-BOTTOM.html')) {
        include $path.'/_DIR-INFO-BOTTOM.html';
    }
	
	echo '</body></html>';
}

/**
 * Function generating index binary file (and index binary file of all subdirectories), containing information about files, directories and their CRC32b hashes in the binary format
 * @param string $path Path to the directory, whose index binary we want
 * @return int CRC32b hash of the generated file (used for recursion calls)
 */
function generateBinaryIndexFile($path) {
	$path = rtrim($path, '/').'/'; //Makes sure the path ends with a slash and file/subdirectory name can be simply appended to it
	$entries = array_diff(scandir($path), array('.', '..'));
    $data = '';
	
    foreach ($entries as $entry) {
        $entryData = pack('N', strlen($entry)); //Length of the string
        $entryData .= pack('A'.strlen($entry), $entry); //Filename (without path, with extension)

        if (is_dir($path.$entry)) {
            //Directory
            $entryData .= "\u{0001}";

            if (file_exists($path.$entry.'/index.dat')) {
                //Index binary of the subdirectory already exists
                $entryData .= pack('J', base_convert(hash_file('crc32b', $path.$entry.'/index.dat'), 16, 10));
            } else {
                //Index binary of the subdirectory doesn't exist yet - generate it recursively
				$entryData .= pack('J', generateBinaryIndexFile($path.$entry));
            }
        } else {
            //File
            $entryData .= "\u{0000}";

            $entryData .= pack('J', base_convert(hash_file('crc32b', $path.$entry), 16, 10));
        }

        $data .= $entryData;
    }

    //Save the file
    $indexBinary = fopen($path.'index.dat', 'w');
    fwrite($indexBinary, $data); //Create and write the index binary file
    fclose($indexBinary);

    //Return index binary's hash
    return base_convert(hash_file('crc32b', $path.'index.dat'), 16, 10);
}

/**
 * Function downloading an existing index binary file for a specified directory.
 * Script execution is killed at the end of this function.
 * @param string $path Path to the directory whose index binary we want to download
 * @return never
 */
function downloadIndexBinary($path) {
    header('Content-Type: application/octet-stream');
    header("Content-Transfer-Encoding: Binary");
    header("Content-disposition: attachment; filename=\"index.dat\"");
    readfile($path.'/index.dat');
    exit();
}

