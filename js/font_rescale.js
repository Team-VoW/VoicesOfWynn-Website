$(function() {
    let $nametags = $(".contributor_name"); //Get all nametags wrapper elements (<p>)
    let nametagCount = $nametags.length;
    for (let i = 0; i < nametagCount; i++) { //Iterate through them
        let $currentNametag = $($nametags[i]);
        let maxWidth = $currentNametag.width(); //Get max width that the nametag can have (should be the same for all)
        let currentWidth = $currentNametag.find("strong").width(); //Get the width that the nametags has with the current font size
        let currentFontSize = $currentNametag.find("strong").css('font-size').substring(0, $currentNametag.find("strong").css('font-size').length - 2); //Get the current font size (remove "px" from the returned value)
        while (currentWidth > maxWidth) {
            currentFontSize -= 1; //Decrease the font size by 1 px
            $currentNametag.find("strong").css("font-size", (currentFontSize + "px")); //Apply it to the nametag
            currentWidth = $currentNametag.find("strong").width(); //Recalculate current width, keep doing this untill the nametag fits
        }
    }
});
