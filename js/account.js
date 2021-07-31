$("#avatar-input").on('change', function(event) {
    $("#avatar-preview").attr('src', URL.createObjectURL(event.target.files[0]));
});