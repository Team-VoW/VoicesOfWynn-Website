$("#download-btn").click(function (){
    $("#download-btn").closest("a").hide();
    $("#download-info").hide();
    $("#download-started").show();
    $("#download-count").text((Number)($("#download-count").text()) + 1);
    $("#report-download-issue").show();
})
