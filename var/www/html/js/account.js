$("#avatar-input").on('change', function(event) {
    $("#avatar-preview").attr('src', URL.createObjectURL(event.target.files[0]));
});

$(function () {
    let $inputs = $(".account-edit-field-left");
    let currentMaxWidth = 0;
    for (let i = 0; i < $inputs.length; i++) 
    { 
      if(currentMaxWidth < Math.ceil($($inputs[i]).outerWidth())) 
      {
        currentMaxWidth = Math.ceil($($inputs[i]).outerWidth());
        console.log(Math.ceil($($inputs[i]).outerWidth()))
      }
      if(i+1 === $inputs.length) 
      {
        for(let h = 0; h < $inputs.length; h++)
        {
          $($inputs[h]).width(currentMaxWidth + "px")
        }
      }
    }
  });