$("#avatar-input").on('change', function(event) {
    $("#avatar-preview").attr('src', URL.createObjectURL(event.target.files[0]));
});

$(function () {
    let $cards = $(".account-edit-field-left");
    let currentMaxWidth = 0;
    for (let i = 0; i < $cards.length; i++) 
    { 
      if(currentMaxWidth < Math.ceil($($cards[i]).outerWidth())) 
      {
        currentMaxWidth = Math.ceil($($cards[i]).outerWidth());
        console.log(Math.ceil($($cards[i]).outerWidth()))
      }
      if(i+1 === $cards.length) 
      {
        for(let h = 0; h < $cards.length; h++)
        {
          $($cards[h]).width(currentMaxWidth + "px")
        }
      }
    }
  });

  $(function () {
    let $cards = $(".input-change");
    let currentMaxWidth = 0;
    for (let i = 0; i < $cards.length; i++) 
    { 
      if(currentMaxWidth < Math.ceil($($cards[i]).outerWidth())) 
      {
        currentMaxWidth = Math.ceil($($cards[i]).outerWidth());
        console.log(Math.ceil($($cards[i]).outerWidth()))
      }
      if(i+1 === $cards.length) 
      {
        for(let h = 0; h < $cards.length; h++)
        {
          $($cards[h]).width(currentMaxWidth + "px")

        }
      }
    }
  });