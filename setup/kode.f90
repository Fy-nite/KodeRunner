
module kode
    implicit none
    public :: greet
contains
    subroutine greet() bind(C, name='greet')
      
    end subroutine greet

    subroutine install()
        use iso_c_binding, only: c_f_pointer, c_ptr, c_char
        implicit none
        integer :: status

        ! clone the git repository
        status = system('git clone https://git.gay/Finite/KodeRunner.git')
        
        if (status /= 0) then
            print *, "Error: Command failed with status ", status
        else
            print *, "Installation successful!"
        end if
    end subroutine install

end module kode




program installer
    implicit none
    PRINT *, "Welcome to the official KodeRunner setup wizard!"
    PRINT *, "Installing KodeRunner..."
    
end program installer