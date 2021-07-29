<?php


namespace VoicesOfWynn\Controllers;


class Rooter extends Controller
{
    
    private const VIEWS_FOLDER = 'Views';
    
    /**
     * Method processing the request
     * Decides which controller to call depending on the requested URL
     * @param array $args Numeric array containing only one element - the requested URL
     */
    public function process(array $args): bool
    {
        // TODO: Implement process() method.
        
        self::$data['base_currentYear'] = date('Y');
        
        return false;
    }
    
    /**
     * Method composing the final website from the list of views and data supplied by specific controllers
     * @return string Final website to send to the user
     */
    public function displayView(): string
    {
        //Sanitize against XSS attack
        $sanitized = array();
        foreach (self::$data as $key => $value) {
            $sanitized[$key] = htmlspecialchars($value);
        }
        extract($sanitized);
        
        ob_start();
        require $this->getNextView();
        $result = ob_get_contents();
        ob_end_clean();
        return $result;
    }
    
    /**
     * Method returning a path to the next (more inner) view to display
     * @return string Path to the view
     */
    public function getNextView(): string
    {
        return self::VIEWS_FOLDER.'/'.array_shift(self::$views).'.phtml';
    }
}

