$("#download-btn").click(function (){
    $("#download-btn").closest("a").hide();
    $("#download-started").show();
    $("#download-count").text((Number)($("#download-count").text()) + 1);
})

$("#install-btn").click(function (){
    $("#install-btn").closest("a").hide();
    $("#install-started").show();
    $("#false-positive-info").css('border', '3px solid red');
    $("#download-count").text((Number)($("#download-count").text()) + 1);
})
