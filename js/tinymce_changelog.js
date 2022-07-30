//Fetch the TinyMCE script using the API key and apply the editor to the bio textarea
$.getScript("https://cdn.tiny.cloud/1/kpiricxjt3ddplrd8k4ex78xpjn6i6iy99gp4lln5p93uo74/tinymce/5/tinymce.min.js",
	function() {
		tinymce.init({
			selector: '#changelog',
			height: 500,
			toolbar: 'undo redo cut copy paste | removeformat | bold italic underline strikethrough | h1 h2 h3 bullist numlist | link image | alignleft aligncenter alignright alignjustify',
			menubar: '',
			contextmenu: '',
			plugins: 'lists link image'
		});
	}
);
