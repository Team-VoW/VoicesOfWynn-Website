<?php

namespace VoicesOfWynn\Models;

class AccountDataValidator
{
    public array $errors = array();
    
    public function validateEmail(string $email): bool
    {
        //TODO
        return true;
    }
    
    public function validatePassword(string $password): bool
    {
        //TODO
        return true;
    }
    
    public function validateName(string $name): bool
    {
        //TODO
        return true;
    }
    
    public function validateAvatar(array $uploadInfo): bool
    {
        if ($uploadInfo['size'] > 1048576) {
            $this->errors[] = 'The profile image must be smaller than 1 MB.';
            return false;
        }
        if ($uploadInfo['type'] !== 'image/png' && $_FILES['avatar']['type'] !== 'image/jpeg') {
            $this->errors[] = 'The profile image must be either .PNG or .JPG.';
            return false;
        }
        if ($uploadInfo['error'] !== 0) {
            $this->errors[] = 'An unknown error occurred during the file upload - try again or ping @Shady#2948 on Discord';
            return false;
        }
        
        return true;
    }
    
    public function validateBio(string $bio): bool
    {
        //TODO
        return true;
    }
}