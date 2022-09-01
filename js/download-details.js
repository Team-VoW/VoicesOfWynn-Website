$("#download-btn").click(function (){
    $("#download-btn").closest("a").hide();
    $("#download-started").show();
    $("#download-count").text((Number)($("#download-count").text()) + 1);
})

$("#install-btn").click(function (){
    $("#install-btn").closest("a").hide();
    $("#install-started").show();
    $("#download-count").text((Number)($("#download-count").text()) + 1);
})
